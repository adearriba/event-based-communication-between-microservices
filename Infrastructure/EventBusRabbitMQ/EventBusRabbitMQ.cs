using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using EventBus.Events;
using EventBus.Extensions;
using EventBus.Interfaces;
using EventBus.SubscriptionManager;
using EventBusRabbitMQ.Connections;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace EventBusRabbitMQ
{
    public class EventBusRabbitMQ : IEventBus, IDisposable
    {
        const string BROKER_NAME = "rabbitmq_event_bus";
        const string AUTOFAC_SCOPE_NAME = "rabbitmq_event_bus";

        private readonly IRabbitMQPersistentConnection _persistentConnection;
        private readonly ILogger<EventBusRabbitMQ> _logger;
        private readonly IEventBusSubscriptionsManager _subsManager;
        private readonly ILifetimeScope _autofac;
        private readonly int _retryCount;
        private IModel _consumerChannel;
        private string _queueName;
        
        public EventBusRabbitMQ(
            IRabbitMQPersistentConnection persistentConnection, 
            ILogger<EventBusRabbitMQ> logger,
            ILifetimeScope autofac, 
            IEventBusSubscriptionsManager subsManager, 
            string queueName = null, int retryCount = 5)
        {
            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _subsManager = subsManager ?? new InMemoryEventBusSubscriptionsManager();

            _queueName = queueName;
            _autofac = autofac;
            _retryCount = retryCount;

            _consumerChannel = CreateConsumerChannel();
            _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
        }
        
        public void Publish(IntegrationEvent @event)
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            var policy = RetryPolicy
                .Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetry(
                    _retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), 
                    (ex, time) =>
                    {
                        _logger.LogWarning(ex, "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                    }
                );
            
            var eventName = @event.GetType().Name;
            _logger.LogTrace($"Creating RabbitMQ channel to publish event: {@event.Id} ({eventName})");

            using (var channel = _persistentConnection.CreateModel())
            {
                _logger.LogTrace($"Declaring RabbitMQ exchange to publish event: {@event.Id}");

                channel.ExchangeDeclare(exchange: BROKER_NAME, type: ExchangeType.Direct);
                var message = JsonConvert.SerializeObject(@event);
                var body = Encoding.UTF8.GetBytes(message);

                policy.Execute(() =>
                {
                    var properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = 2;

                    _logger.LogTrace($"Publishing event to RabbitMQ: {@event.Id}");

                    channel.BasicPublish(
                        exchange: BROKER_NAME,
                        routingKey: eventName,
                        mandatory: true,
                        basicProperties: properties,
                        body: body);
                });
            }
        }

        public void Subscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();
            var eventHandler = typeof(TH).GetGenericTypeName();

            if (!_subsManager.HasSubscriptionsForEvent(eventName))
            {
                if (!_persistentConnection.IsConnected)
                {
                    _persistentConnection.TryConnect();
                }

                using (var channel = _persistentConnection.CreateModel())
                {
                    channel.QueueBind(queue: _queueName,
                                      exchange: BROKER_NAME,
                                      routingKey: eventName);
                }
            }

            _logger.LogInformation($"Subscribing to event {eventName} with {eventHandler}");
            _subsManager.AddSubscription<T, TH>();
            StartBasicConsume();
        }

        public void Unsubscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();

            _logger.LogInformation($"Unsubscribing from event {eventName}");
            _subsManager.RemoveSubscription<T, TH>();
        }

        public void Dispose()
        {
            if (_consumerChannel != null)
            {
                _consumerChannel.Dispose();
            }

            if (_persistentConnection != null)
            {
                _persistentConnection.Dispose();
            }

            _subsManager.Clear();
        }

        private void SubsManager_OnEventRemoved(object sender, string eventName)
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            using (var channel = _persistentConnection.CreateModel())
            {
                channel.QueueUnbind(queue: _queueName,
                    exchange: BROKER_NAME,
                    routingKey: eventName);

                if (_subsManager.IsEmpty)
                {
                    _queueName = string.Empty;
                    _consumerChannel.Close();
                }
            }
        }

        private IModel CreateConsumerChannel()
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            _logger.LogTrace("Creating RabbitMQ consumer channel");

            var channel = _persistentConnection.CreateModel();
            channel.ExchangeDeclare(exchange: BROKER_NAME, type: ExchangeType.Direct);

            IDictionary<String, Object> args = new Dictionary<String, Object>();
            args.Add("x-dead-letter-exchange", BROKER_NAME);

            channel.QueueDeclare(queue: _queueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: args);

            channel.CallbackException += (sender, ea) =>
            {
                _logger.LogWarning(ea.Exception, "Recreating RabbitMQ consumer channel");

                _consumerChannel.Dispose();
                _consumerChannel = CreateConsumerChannel();
                StartBasicConsume();
            };

            return channel;
        }

        private void StartBasicConsume()
        {
            _logger.LogTrace("Starting RabbitMQ basic consume");

            if (_consumerChannel != null)
            {
                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);

                consumer.Received += Consumer_Received;

                _consumerChannel.BasicConsume(
                    queue: _queueName,
                    autoAck: false,
                    consumer: consumer);
            }
            else
            {
                _logger.LogError("StartBasicConsume can't call on _consumerChannel == null");
            }
        }

        private async Task Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
        {
            var eventName = eventArgs.RoutingKey;
            var message = Encoding.UTF8.GetString(eventArgs.Body.Span);

            try
            {
                if (message.ToLowerInvariant().Contains("throw-fake-exception"))
                {
                    throw new InvalidOperationException($"Fake exception requested: \"{message}\"");
                }

                await ProcessEvent(eventName, message);
                _consumerChannel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"----- ERROR Processing message. Requeuing: \"{message}\"");
                _consumerChannel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: true);
            }
        }

        private async Task ProcessEvent(string eventName, string message)
        {
            _logger.LogTrace($"Processing RabbitMQ event: {eventName}");

            if (_subsManager.HasSubscriptionsForEvent(eventName))
            {
                using (var scope = _autofac.BeginLifetimeScope(AUTOFAC_SCOPE_NAME))
                {
                    var subscriptions = _subsManager.GetHandlersForEvent(eventName);
                    foreach (var subscription in subscriptions)
                    {
                        var handler = scope.ResolveOptional(subscription.HandlerType);
                        if (handler == null) continue;
                        
                        var eventType = _subsManager.GetEventTypeByName(eventName);
                        var integrationEvent = JsonConvert.DeserializeObject(message, eventType);
                        var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

                        await Task.Yield();
                        await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent });
                    }
                }
            }
            else
            {
                _logger.LogWarning("No subscription for RabbitMQ event: {EventName}", eventName);
            }
        }
    }
}