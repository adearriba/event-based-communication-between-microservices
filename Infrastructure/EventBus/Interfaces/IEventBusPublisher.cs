using EventBus.Events;

namespace EventBus.Interfaces
{
    public interface IEventBusPublisher
    {
        void Publish(IntegrationEvent @event);
    }
}