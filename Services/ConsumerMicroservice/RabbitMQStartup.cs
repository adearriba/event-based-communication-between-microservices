using System;
using Autofac;
using ConsumerMicroservice.IntegrationEvents.EventHandlers;
using ConsumerMicroservice.IntegrationEvents.Events;
using EventBus.Interfaces;
using EventBus.SubscriptionManager;
using EventBusRabbitMQ.Connections;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ConsumerMicroservice
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

        public static IEventBus CreateEventBus(
            IConfiguration configuration, IServiceProvider serviceProvider)
        {
            var subscriptionClientName = configuration["SubscriptionClientName"];
            var rabbitMQPersistentConnection = serviceProvider.GetRequiredService<IRabbitMQPersistentConnection>();
            var iLifetimeScope = serviceProvider.GetRequiredService<ILifetimeScope>();
            var logger = serviceProvider.GetRequiredService<ILogger<EventBusRabbitMQ.EventBusRabbitMQ>>();
            var eventBusSubcriptionsManager = serviceProvider.GetRequiredService<IEventBusSubscriptionsManager>();

            var retryCount = 5;
            if (!string.IsNullOrEmpty(configuration["EventBusRetryCount"]))
            {
                retryCount = int.Parse(configuration["EventBusRetryCount"]);
            }

            return new EventBusRabbitMQ.EventBusRabbitMQ(rabbitMQPersistentConnection, logger, iLifetimeScope, eventBusSubcriptionsManager, subscriptionClientName, retryCount);
        }

        public static void ConfigureEventBus(IApplicationBuilder app)
        {
            var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
            eventBus.Subscribe<WeatherForecastRequestedIntegrationEvent, WeatherForecastRequestedHandler>();
        }
    }
}
