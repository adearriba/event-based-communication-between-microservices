using EventBus.Events;

namespace EventBus.Interfaces
{
    public interface IEventBus : IEventBusPublisher, IEventBusSubscriber
    {
        
    }
}