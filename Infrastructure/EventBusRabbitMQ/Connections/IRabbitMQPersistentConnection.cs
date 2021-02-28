using System;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Connections
{
    public interface IRabbitMQPersistentConnection : IDisposable
    {
        bool IsConnected { get; }

        bool TryConnect();
         
        IModel CreateModel();
    }
}
