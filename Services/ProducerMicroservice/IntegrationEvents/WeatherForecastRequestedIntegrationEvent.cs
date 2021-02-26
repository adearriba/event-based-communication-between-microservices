using System.Collections.Generic;
using EventBus.Events;
using ProducerMicroservice.Models;

namespace ProducerMicroservice.IntegrationEvents
{
    public record WeatherForecastRequestedIntegrationEvent: IntegrationEvent
    {
        public IEnumerable<WeatherForecast> ForecastResult { get; init; }

        public WeatherForecastRequestedIntegrationEvent(IEnumerable<WeatherForecast> forecast)
        {
            ForecastResult = forecast;
        }
    }
}
