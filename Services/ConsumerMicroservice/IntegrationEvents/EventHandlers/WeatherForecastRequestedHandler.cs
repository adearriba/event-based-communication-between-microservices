using System.Threading.Tasks;
using ConsumerMicroservice.IntegrationEvents.Events;
using EventBus.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ConsumerMicroservice.IntegrationEvents.EventHandlers
{
    public class WeatherForecastRequestedHandler :
        IIntegrationEventHandler<WeatherForecastRequestedIntegrationEvent>
    {
        private readonly ILogger<WeatherForecastRequestedHandler> _logger;

        public WeatherForecastRequestedHandler(ILogger<WeatherForecastRequestedHandler> logger)
        {
            _logger = logger;
        }

        public Task Handle(WeatherForecastRequestedIntegrationEvent @event)
        {
            var message = JsonConvert.SerializeObject(@event);
            _logger.LogInformation($"Menssage received: {message}");

            return Task.CompletedTask;
        }
    }
}
