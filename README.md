
# Event-based Communication between microservices

This project is inspired in Microsoft´s  [eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers). Using a RabbitMQ container.

## Running in docker
Build the whole solution with:
``docker-compose build``

And then run the solution with:
``docker-compose up``

## Test communication
Create a request to ``ProducerMicroservice`` WeatherForecast endpoint:
http://host.docker.internal:5001/WeatherForecast

The ``WeatherForecastController.cs`` publishes an event to the event bus in the GET request:
```csharp
...
var eventMessage = new WeatherForecastRequestedIntegrationEvent(forecast);

try{
    _eventBus.Publish(eventMessage);
}
catch (Exception ex)
{
    _logger.LogError(ex, $"ERROR Publishing integration event: {eventMessage.Id} from {this.GetType().Name}");
    throw;
}
...
```
The ``ConsumerMicroservice`` has registered a handler for that event type: 

``Startup.cs``
```csharp
services.AddTransient<WeatherForecastRequestedHandler>();
RabbitMQStartup.ConfigureEventBus(app);
```

``RabbitMQStartup.cs``
```csharp
public static void ConfigureEventBus(IApplicationBuilder app)
{
    var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
    eventBus.Subscribe<WeatherForecastRequestedIntegrationEvent, WeatherForecastRequestedHandler>();
}
```

View in the console that ConsumerMicroservice received a message with the forecast data.

## Inside RabbitMQ
You can access RabbitMQ web panel using:
``http://host.docker.internal:15672/``
**User:** guest
**Password:** guest
