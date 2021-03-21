using EventBus.AzureServiceBus.Connections;
using EventBus.Events;
using EventBus.Interfaces;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Text;

namespace EventBus.AzureServiceBus
{
    public class AzureServiceBusPublisher : IEventBusPublisher
    {
        private readonly IServiceBusPersistentConnection _persistentConnection;
        private readonly ILogger<AzureServiceBusPublisher> _logger;
        private readonly AzureServiceBusSettings _settings;

        public AzureServiceBusPublisher(IServiceBusPersistentConnection persistentConnection, ILogger<AzureServiceBusPublisher> logger)
        {
            _persistentConnection = persistentConnection;
            _logger = logger;
            _settings = AzureServiceBusSettings.GetInstance();
        }

        public void Publish(IntegrationEvent @event)
        {
            var eventName = @event.GetType().Name.Replace(_settings.IntegrationEventSuffix, "");
            var jsonMessage = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(jsonMessage);

            var message = new Message
            {
                MessageId = Guid.NewGuid().ToString(),
                Body = body,
                Label = eventName,
            };

            _persistentConnection.TopicClient
                .SendAsync(message).GetAwaiter().GetResult();

            _logger.LogInformation($"Published event: {eventName}.");
        }
    }
}
