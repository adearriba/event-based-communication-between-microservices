
# Event-based Communication between microservices

This project is inspired in Microsoft´s  [eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers). Using a RabbitMQ container.

## Running in docker
Build the solution:
``docker-compose build``

Run the solution with Linux containers:
``docker-compose up``

Or Windows containers:
``docker-compose -f docker-compose.windows.yml -f docker-compose.override.windows.yml up``

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
![Console logs](https://raw.githubusercontent.com/adearriba/event-based-communication-between-microservices/84efd3a3815c28b45684b5e2d3f2361b71d26a6c/img/console_events_logs.png "Console Event Log")

## Inside RabbitMQ
You can access RabbitMQ web panel using:
``http://host.docker.internal:15672/``

**User:** guest

**Password:** guest
