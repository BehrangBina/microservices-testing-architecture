using PaymentService.Events;

namespace PaymentService.Producers;

public interface IPaymentEventProducer
{
    Task PublishPaymentProcessedAsync(PaymentProcessedEvent evt, CancellationToken ct = default);
}
