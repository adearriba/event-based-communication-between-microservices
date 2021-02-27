using EventBus.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProducerMicroservice.IntegrationEvents;
using ProducerMicroservice.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProducerMicroservice.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FakeErrorController : ControllerBase
    {
        private readonly IEventBusPublisher _eventBus;
        private readonly ILogger<WeatherForecastController> _logger;

        public FakeErrorController(IEventBusPublisher eventBus, ILogger<WeatherForecastController> logger)
        {
            _eventBus = eventBus;
            _logger = logger;
        }

        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {
            var rng = new Random();
            var forecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = "throw-fake-exception"
            })
            .ToArray();

            var eventMessage = new WeatherForecastRequestedIntegrationEvent(forecast);
            try
            {
                _eventBus.Publish(eventMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ERROR Publishing integration event: {eventMessage.Id} from {this.GetType().Name}");
                throw;
            }

            return forecast;
        }

    }
}
