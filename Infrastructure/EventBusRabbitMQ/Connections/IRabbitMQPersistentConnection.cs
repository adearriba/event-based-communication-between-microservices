using System;
using RabbitMQ.Client;

namespace EventBusRabbitMQ.Connections
{
    public interface IRabbitMQPersistentConnection : IDisposable
    {
        bool IsConnected { get; }

        bool TryConnect();
         
        IModel CreateModel();
    }
}
