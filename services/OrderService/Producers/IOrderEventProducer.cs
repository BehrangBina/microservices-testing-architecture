using OrderService.Events;

namespace OrderService.Producers;

public interface IOrderEventProducer
{
    Task PublishOrderCreatedAsync(OrderCreatedEvent evt, CancellationToken ct = default);
}
