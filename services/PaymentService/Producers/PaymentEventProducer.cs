using Confluent.Kafka;
using PaymentService.Events;
using System.Text.Json;

namespace PaymentService.Producers;

public class PaymentEventProducer : IPaymentEventProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private const string Topic = "payment-processed";

    public PaymentEventProducer(IConfiguration configuration)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092"
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishPaymentProcessedAsync(PaymentProcessedEvent evt, CancellationToken ct = default)
    {
        var message = new Message<string, string>
        {
            Key = evt.OrderId.ToString(),
            Value = JsonSerializer.Serialize(evt)
        };
        await _producer.ProduceAsync(Topic, message, ct);
    }

    public void Dispose() => _producer.Dispose();
}
