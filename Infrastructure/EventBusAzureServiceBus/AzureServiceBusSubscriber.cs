using Autofac;
using EventBus.AzureServiceBus.Connections;
using EventBus.Events;
using EventBus.Interfaces;
using EventBus.SubscriptionManager;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.AzureServiceBus
{
    public class AzureServiceBusSubscriber : IEventBusSubscriber, IDisposable
    {
        const string AUTOFAC_SCOPE_NAME = "azure_event_bus";
        private readonly IServiceBusPersistentConnection _persistentConnection;
        private readonly IEventBusSubscriptionsManager _subsManager;
        private readonly ILogger<AzureServiceBusSubscriber> _logger;
        private readonly ILifetimeScope _autofac;
        private readonly AzureServiceBusSettings _settings;

        public AzureServiceBusSubscriber(IServiceBusPersistentConnection persistentConnection, 
            IEventBusSubscriptionsManager subsManager, 
            ILogger<AzureServiceBusSubscriber> logger, 
            ILifetimeScope autofac)
        {
            _persistentConnection = persistentConnection;
            _subsManager = subsManager;
            _logger = logger;
            _autofac = autofac;

            _settings = AzureServiceBusSettings.GetInstance();

            RemoveDefaultRule();
            RegisterSubscriptionClientMessageHandler();
        }

        public void Subscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = typeof(T).Name.Replace(_settings.IntegrationEventSuffix, "");
            var containsKey = _subsManager.HasSubscriptionsForEvent<T>();

            if (!containsKey)
            {
                try
                {
                    _persistentConnection.SubscriptionClient.AddRuleAsync(new RuleDescription
                    {
                        Filter = new CorrelationFilter { Label = eventName },
                        Name = eventName
                    }).GetAwaiter().GetResult();
                }
                catch (ServiceBusException)
                {
                    _logger.LogWarning("The messaging entity {eventName} already exists.", eventName);
                }
            }

            _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).Name);
            _subsManager.AddSubscription<T, TH>();
        }

        public void Unsubscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = typeof(T).Name.Replace(_settings.IntegrationEventSuffix, "");
            try
            {
                _persistentConnection
                    .SubscriptionClient
                    .RemoveRuleAsync(eventName)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (MessagingEntityNotFoundException)
            {
                _logger.LogWarning("The messaging entity {eventName} Could not be found.", eventName);
            }

            _logger.LogInformation("Unsubscribing from event {EventName}", eventName);
            _subsManager.RemoveSubscription<T, TH>();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private void RegisterSubscriptionClientMessageHandler()
        {
            _persistentConnection.SubscriptionClient.RegisterMessageHandler(
                async (message, token) =>
                {
                    var eventName = $"{message.Label}{_settings.IntegrationEventSuffix}";
                    var messageData = Encoding.UTF8.GetString(message.Body);

                    if (await ProcessEvent(eventName, messageData))
                    {
                        await _persistentConnection
                            .SubscriptionClient
                            .CompleteAsync(message.SystemProperties.LockToken);
                    }
                },
                new MessageHandlerOptions(ExceptionReceivedHandler) { 
                    MaxConcurrentCalls = 10, 
                    AutoComplete = false 
                });
        }

        private async Task<bool> ProcessEvent(string eventName, string message)
        {
            var processed = false;
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

                        await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent });
                    }
                }
                processed = true;
            }
            return processed;
        }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            var ex = exceptionReceivedEventArgs.Exception;
            var context = exceptionReceivedEventArgs.ExceptionReceivedContext;

            _logger.LogError(ex, "ERROR handling message: {ExceptionMessage} - Context: {@ExceptionContext}", ex.Message, context);

            return Task.CompletedTask;
        }

        private void RemoveDefaultRule()
        {
            try
            {
                _persistentConnection
                    .SubscriptionClient
                    .RemoveRuleAsync(RuleDescription.DefaultRuleName)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (MessagingEntityNotFoundException)
            {
                _logger.LogWarning("The messaging entity {DefaultRuleName} Could not be found.", RuleDescription.DefaultRuleName);
            }
        }
    }
}
