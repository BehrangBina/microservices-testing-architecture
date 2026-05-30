using Confluent.Kafka;
using OrderService.Events;
using System.Text.Json;

namespace OrderService.Producers;

public class OrderEventProducer : IOrderEventProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private const string Topic = "order-created";

    public OrderEventProducer(IConfiguration configuration)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092"
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishOrderCreatedAsync(OrderCreatedEvent evt, CancellationToken ct = default)
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
