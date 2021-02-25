using System;
using System.Collections.Generic;
using EventBus.Events;
using EventBus.Interfaces;

namespace EventBus.SubscriptionManager
{
    public interface IEventBusSubscriptionsManager
    {
        event EventHandler<string> OnEventRemoved;

        void AddSubscription<T, TH>()
           where T : IntegrationEvent
           where TH : IIntegrationEventHandler<T>;

        void RemoveSubscription<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        bool HasSubscriptionsForEvent<T>() where T : IntegrationEvent;
        bool HasSubscriptionsForEvent(string eventName);
        
        IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>() where T : IntegrationEvent;
        IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName);

        bool IsEmpty { get; }

        void Clear();

        string GetEventKey<T>();
    }
}