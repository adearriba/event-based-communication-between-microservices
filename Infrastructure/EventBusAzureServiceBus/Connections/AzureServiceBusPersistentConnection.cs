using Microsoft.Azure.ServiceBus;
using System;

namespace EventBus.AzureServiceBus.Connections
{
    public class AzureServiceBusPersistentConnection : IServiceBusPersistentConnection
    {
        private readonly ServiceBusConnectionStringBuilder _serviceBusConnectionStringBuilder;
        private readonly string _subscriptionClientName;
        private ISubscriptionClient _subscriptionClient;
        private ITopicClient _topicClient;
        private bool _disposed;

        public AzureServiceBusPersistentConnection(ServiceBusConnectionStringBuilder serviceBusConnectionStringBuilder,
            string subscriptionClientName)
        {
            _serviceBusConnectionStringBuilder = serviceBusConnectionStringBuilder ??
                throw new ArgumentNullException(nameof(serviceBusConnectionStringBuilder));
            _subscriptionClientName = subscriptionClientName;

            _subscriptionClient = new SubscriptionClient(_serviceBusConnectionStringBuilder, _subscriptionClientName);
            _topicClient = new TopicClient(_serviceBusConnectionStringBuilder, RetryPolicy.Default);
        }

        public ITopicClient TopicClient
        {
            get
            {
                if (_topicClient.IsClosedOrClosing)
                {
                    _topicClient = new TopicClient(_serviceBusConnectionStringBuilder, RetryPolicy.Default);
                }
                return _topicClient;
            }
        }

        public ISubscriptionClient SubscriptionClient
        {
            get
            {
                if (_subscriptionClient.IsClosedOrClosing)
                {
                    _subscriptionClient = new SubscriptionClient(_serviceBusConnectionStringBuilder, _subscriptionClientName);
                }
                return _subscriptionClient;
            }
        }

        public ServiceBusConnectionStringBuilder ServiceBusConnectionStringBuilder => _serviceBusConnectionStringBuilder;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
