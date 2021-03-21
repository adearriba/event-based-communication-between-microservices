using Microsoft.Azure.ServiceBus;
using System;

namespace EventBus.AzureServiceBus.Connections
{
    public interface IServiceBusPersistentConnection : IDisposable
    {
        ITopicClient TopicClient { get; }
        ISubscriptionClient SubscriptionClient { get; }
    }
}
