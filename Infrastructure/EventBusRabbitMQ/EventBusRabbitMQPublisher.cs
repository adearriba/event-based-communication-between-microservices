using System;
using System.Net.Sockets;
using System.Text;
using EventBus.Events;
using EventBus.Interfaces;
using EventBusRabbitMQ.Connections;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace EventBusRabbitMQ
{
    public class EventBusRabbitMQPublisher : IEventBusPublisher, IDisposable
    {
        private readonly IRabbitMQPersistentConnection _persistentConnection;
        private readonly ILogger<EventBusRabbitMQPublisher> _logger;
        private readonly EventBusSettings _settings;
        private readonly int _retryCount;

        public EventBusRabbitMQPublisher(IRabbitMQPersistentConnection persistentConnection, ILogger<EventBusRabbitMQPublisher> logger, int retryCount = 5)
        {
            _persistentConnection = persistentConnection;
            _logger = logger;
            _settings = EventBusSettings.GetInstance();
            _retryCount = retryCount;
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

                channel.ExchangeDeclare(exchange: _settings.DefaultExchangeName, type: ExchangeType.Direct);
                var message = JsonConvert.SerializeObject(@event);
                var body = Encoding.UTF8.GetBytes(message);

                policy.Execute(() =>
                {
                    var properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = 2;

                    _logger.LogTrace($"Publishing event to RabbitMQ: {@event.Id}");

                    channel.BasicPublish(
                        exchange: _settings.DefaultExchangeName,
                        routingKey: eventName,
                        mandatory: true,
                        basicProperties: properties,
                        body: body);
                });
            }
        }

        public void Dispose()
        {
            _persistentConnection?.Dispose();
        }
    }
}