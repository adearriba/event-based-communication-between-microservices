using System.Collections.Generic;
using EventBus.Events;

namespace ConsumerMicroservice.IntegrationEvents.Events
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
