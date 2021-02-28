
# Event-based Communication between microservices <!-- omit in toc -->

This project is inspired in Microsoft´s  [eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers). Using a RabbitMQ container.

- [Running in docker](#running-in-docker)
- [Test successful communication](#test-successful-communication)
- [Test dead letter communication](#test-dead-letter-communication)
- [Inside RabbitMQ](#inside-rabbitmq)

## Running in docker
Build the solution:
``docker-compose build``

Run the solution with Linux containers:
``docker-compose up``

Or Windows containers:
``docker-compose -f docker-compose.windows.yml -f docker-compose.override.windows.yml up``

## Test successful communication
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
public static void ConfigureEventBusSubscriber(IApplicationBuilder app)
{
    var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriber>();
    eventBus.Subscribe<WeatherForecastRequestedIntegrationEvent, WeatherForecastRequestedHandler>();
}
```

View in the console that ConsumerMicroservice received a message with the forecast data.
![Console logs](https://raw.githubusercontent.com/adearriba/event-based-communication-between-microservices/84efd3a3815c28b45684b5e2d3f2361b71d26a6c/img/console_events_logs.png "Console Event Log")

## Test dead letter communication
Create a request to ``ProducerMicroservice`` WeatherForecast endpoint:
http://host.docker.internal:5001/FakeError

The ``FakeErrorController.cs`` publishes an event to the event bus with the "throw-fake-exception" string in the GET request:
```csharp
...
WeatherForecast
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
...
```
The ``ConsumerMicroservice`` has registered a handler the dead letter event: 

``Startup.cs``
```csharp
//Handler
services.AddTransient<WeatherForecastRequestedDeadLetterHandler>();

//Subscriber
services.AddSingleton<IEventBusDeadLetterSubscriber>(sp => RabbitMQStartup.CreateEventBusDeadLetterSubscriber(sp));

//Add event to subscriber manager
RabbitMQStartup.ConfigureEventBusDeadLetterSubscriber(app);
```

``RabbitMQStartup.cs``
```csharp
public static void ConfigureEventBusDeadLetterSubscriber(IApplicationBuilder app)
{
    var eventBus = app.ApplicationServices.GetRequiredService<IEventBusDeadLetterSubscriber>();
    eventBus.Subscribe<WeatherForecastRequestedIntegrationEvent, WeatherForecastRequestedDeadLetterHandler>();
}
```

View in the console that ConsumerMicroservice received an error message and handled the dead letter event.
![Console dead letter logs](https://github.com/adearriba/event-based-communication-between-microservices/blob/05475e1cfaaf0eaca6aea9fdc950e67cadaa7ba8/img/console_events_logs_rejected.png?raw=true "Console dead letter event Log")

## Inside RabbitMQ
You can access RabbitMQ web panel using:
``http://host.docker.internal:15672/``

**User:** guest

**Password:** guest
