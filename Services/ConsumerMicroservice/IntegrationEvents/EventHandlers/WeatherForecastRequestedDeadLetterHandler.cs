using System.Threading.Tasks;
using ConsumerMicroservice.IntegrationEvents.Events;
using EventBus.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ConsumerMicroservice.IntegrationEvents.EventHandlers
{
    public class WeatherForecastRequestedDeadLetterHandler :
        IIntegrationEventHandler<WeatherForecastRequestedIntegrationEvent>
    {
        private readonly ILogger<WeatherForecastRequestedDeadLetterHandler> _logger;

        public WeatherForecastRequestedDeadLetterHandler(ILogger<WeatherForecastRequestedDeadLetterHandler> logger)
        {
            _logger = logger;
        }

        public Task Handle(WeatherForecastRequestedIntegrationEvent @event)
        {
            var message = JsonConvert.SerializeObject(@event);
            _logger.LogInformation($"Dead Letter Menssage received: {message}");

            return Task.CompletedTask;
        }
    }
}
