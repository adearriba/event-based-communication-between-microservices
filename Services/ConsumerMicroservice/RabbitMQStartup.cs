using System;
using Autofac;
using ConsumerMicroservice.IntegrationEvents.EventHandlers;
using ConsumerMicroservice.IntegrationEvents.Events;
using EventBus.Interfaces;
using EventBus.SubscriptionManager;
using EventBus.RabbitMQ;
using EventBus.RabbitMQ.Connections;
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

        public static IEventBusSubscriber CreateEventBusSubscriber(
            IConfiguration configuration, IServiceProvider serviceProvider)
        {
            var queueName = configuration["SubscriptionClientName"];
            var rabbitMQPersistentConnection = serviceProvider.GetRequiredService<IRabbitMQPersistentConnection>();
            var iLifetimeScope = serviceProvider.GetRequiredService<ILifetimeScope>();
            var logger = serviceProvider.GetRequiredService<ILogger<RabbitMQSubscriber>>();
            var eventBusSubcriptionsManager = serviceProvider.GetRequiredService<IEventBusSubscriptionsManager>();

            return new RabbitMQSubscriber(rabbitMQPersistentConnection, logger, iLifetimeScope, eventBusSubcriptionsManager, queueName);
        }

        public static void ConfigureEventBusSubscriber(IApplicationBuilder app)
        {
            var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriber>();
            eventBus.Subscribe<WeatherForecastRequestedIntegrationEvent, WeatherForecastRequestedHandler>();
        }

        public static IEventBusDeadLetterSubscriber CreateEventBusDeadLetterSubscriber(IServiceProvider serviceProvider)
        {
            var rabbitMQPersistentConnection = serviceProvider.GetRequiredService<IRabbitMQPersistentConnection>();
            var iLifetimeScope = serviceProvider.GetRequiredService<ILifetimeScope>();
            var logger = serviceProvider.GetRequiredService<ILogger<RabbitMQSubscriber>>();
            var eventBusSubcriptionsManager = serviceProvider.GetRequiredService<IEventBusDeadLetterSubscriptionsManager>();

            return new RabbitMQDeadLetterSubscriber(rabbitMQPersistentConnection, logger, iLifetimeScope, eventBusSubcriptionsManager);
        }

        public static void ConfigureEventBusDeadLetterSubscriber(IApplicationBuilder app)
        {
            var eventBus = app.ApplicationServices.GetRequiredService<IEventBusDeadLetterSubscriber>();
            eventBus.Subscribe<WeatherForecastRequestedIntegrationEvent, WeatherForecastRequestedDeadLetterHandler>();
        }
    }
}
