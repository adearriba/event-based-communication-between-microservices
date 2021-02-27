using System;
using Autofac;
using EventBus.Interfaces;
using EventBus.SubscriptionManager;
using EventBusRabbitMQ;
using EventBusRabbitMQ.Connections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ProducerMicroservice
{
    public class RabbitMQStartup
    {
        public static IRabbitMQPersistentConnection CreateDefaultPersistentConnection(
            IConfiguration configuration, IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();

            var factory = new ConnectionFactory()
            {
                HostName = configuration["EventBusConnection"],
                DispatchConsumersAsync = true
            };

            if (!string.IsNullOrEmpty(configuration["EventBusUserName"]))
            {
                factory.UserName = configuration["EventBusUserName"];
            }

            if (!string.IsNullOrEmpty(configuration["EventBusPassword"]))
            {
                factory.Password = configuration["EventBusPassword"];
            }

            var retryCount = 5;
            if (!string.IsNullOrEmpty(configuration["EventBusRetryCount"]))
            {
                retryCount = int.Parse(configuration["EventBusRetryCount"]);
            }

            return new DefaultRabbitMQPersistentConnection(factory, logger, retryCount);
        }

        public static IEventBusPublisher CreateEventBusPublisher(
            IConfiguration configuration, IServiceProvider serviceProvider)
        {
            var rabbitMQPersistentConnection = serviceProvider.GetRequiredService<IRabbitMQPersistentConnection>();
            var logger = serviceProvider.GetRequiredService<ILogger<EventBusRabbitMQPublisher>>();

            var retryCount = 5;
            if (!string.IsNullOrEmpty(configuration["EventBusRetryCount"]))
            {
                retryCount = int.Parse(configuration["EventBusRetryCount"]);
            }

            return new EventBusRabbitMQPublisher(rabbitMQPersistentConnection, logger, retryCount);
        }
    }
}
