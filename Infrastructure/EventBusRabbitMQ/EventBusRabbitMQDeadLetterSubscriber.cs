using System;
using System.Collections.Generic;
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
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EventBusRabbitMQ
{
    public class EventBusRabbitMQDeadLetterSubscriber : IEventBusDeadLetterSubscriber, IDisposable
    {
        const string AUTOFAC_SCOPE_NAME = "rabbitmq_deadletter_event_bus";
        private readonly IRabbitMQPersistentConnection _persistentConnection;
        private readonly IEventBusDeadLetterSubscriptionsManager _subsManager;
        private readonly ILogger<EventBusRabbitMQSubscriber> _logger;
        private readonly ILifetimeScope _autofac;
        private readonly EventBusSettings _settings;
        private IModel _consumerChannel;

        public EventBusRabbitMQDeadLetterSubscriber(
            IRabbitMQPersistentConnection persistentConnection, 
            ILogger<EventBusRabbitMQSubscriber> logger,
            ILifetimeScope autofac,
            IEventBusDeadLetterSubscriptionsManager subsManager)
        {
            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _subsManager = subsManager ?? new InMemoryEventBusSubscriptionsManager();

            _autofac = autofac;
            _settings = EventBusSettings.GetInstance();

            _consumerChannel = CreateConsumerChannel();
            _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
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
                    channel.QueueBind(queue: _settings.DeadLetterQueueName,
                                exchange: _settings.DeadLetterExchangeName,
                                routingKey: eventName);
                }
            }

            _logger.LogInformation($"Subscribing to dead letter event {eventName} with {eventHandler}");
            _subsManager.AddSubscription<T, TH>();

            StartBasicConsumer();
        }

        public void Unsubscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();

            _logger.LogInformation($"Unsubscribing from dead letter event {eventName}");
            _subsManager.RemoveSubscription<T, TH>();
        }

        public void Dispose()
        {
            _consumerChannel?.Dispose();
            _persistentConnection?.Dispose();
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
                channel.QueueUnbind(queue: _settings.DeadLetterQueueName,
                    exchange: _settings.DeadLetterExchangeName,
                    routingKey: eventName);

                if (_subsManager.IsEmpty)
                {
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

            _logger.LogTrace("Creating RabbitMQ dead letter consumer channel");

            var channel = _persistentConnection.CreateModel();
            channel.ExchangeDeclare(exchange: _settings.DeadLetterExchangeName, type: ExchangeType.Direct);
            channel.QueueDeclare(queue: _settings.DeadLetterQueueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            channel.CallbackException += (sender, ea) =>
            {
                _logger.LogWarning(ea.Exception, "Recreating RabbitMQ consumer channel");

                _consumerChannel.Dispose();
                _consumerChannel = CreateConsumerChannel();
                StartBasicConsumer();
            };

            return channel;
        }

        private void StartBasicConsumer()
        {
            _logger.LogTrace("Starting RabbitMQ dead letter basic consumer");

            if (_consumerChannel != null)
            {
                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);

                consumer.Received += MessageReceived;

                _consumerChannel.BasicConsume(
                    queue: _settings.DeadLetterQueueName,
                    autoAck: false,
                    consumer: consumer);
            }
            else
            {
                _logger.LogError("StartBasicConsume from dead letter can't call on _consumerChannel == null");
            }
        }

        private async Task MessageReceived(object sender, BasicDeliverEventArgs eventArgs)
        {
            var eventName = eventArgs.RoutingKey;
            var message = Encoding.UTF8.GetString(eventArgs.Body.Span);
            var reason = Encoding.UTF8.GetString((byte[])eventArgs.BasicProperties.Headers["x-first-death-reason"]);

            _logger.LogInformation($"Dead Letter Menssage received. Reason: {reason}");

            try
            {
                await ProcessEvent(eventName, message);
                _consumerChannel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"----- ERROR Processing message. Message: \"{message}\"");
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