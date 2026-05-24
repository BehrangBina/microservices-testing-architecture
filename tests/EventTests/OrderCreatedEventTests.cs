using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using Xunit;

namespace EventTests;

/// <summary>
/// Tests that the OrderCreated event can be produced to and consumed from Kafka,
/// and that all expected fields are present and correctly typed.
/// </summary>
public class OrderCreatedEventTests : IClassFixture<KafkaTestFixture>
{
    private readonly KafkaTestFixture _kafka;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public OrderCreatedEventTests(KafkaTestFixture kafka) => _kafka = kafka;

    [Fact]
    public async Task OrderCreatedEvent_ProducedAndConsumed_FieldsAreValid()
    {
        // Arrange
        var evt = new
        {
            orderId     = Guid.NewGuid(),
            userId      = Guid.NewGuid(),
            productName = "Wireless Keyboard",
            amount      = 89.99m,
            createdAt   = DateTime.UtcNow
        };

        using var producer = _kafka.CreateProducer();
        using var consumer = _kafka.CreateConsumer($"test-group-{Guid.NewGuid():N}");
        consumer.Subscribe(_kafka.OrderCreatedTopic);

        // Act — produce
        await producer.ProduceAsync(_kafka.OrderCreatedTopic, new Message<string, string>
        {
            Key   = evt.orderId.ToString(),
            Value = JsonSerializer.Serialize(evt, JsonOpts)
        });

        // Act — consume (with 10s timeout)
        var result = consumer.Consume(TimeSpan.FromSeconds(10));
        consumer.Close();

        // Assert
        result.Should().NotBeNull("message should have been consumed within 10s");
        result.Message.Key.Should().Be(evt.orderId.ToString());

        var consumed = JsonSerializer.Deserialize<OrderCreatedDto>(result.Message.Value, JsonOpts)!;
        consumed.OrderId.Should().Be(evt.orderId);
        consumed.UserId.Should().NotBe(Guid.Empty);
        consumed.ProductName.Should().NotBeNullOrEmpty();
        consumed.Amount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task OrderCreatedEvent_MessageKey_EqualsOrderId()
    {
        var orderId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            orderId,
            userId      = Guid.NewGuid(),
            productName = "Monitor",
            amount      = 349.00m,
            createdAt   = DateTime.UtcNow
        }, JsonOpts);

        using var producer = _kafka.CreateProducer();
        await producer.ProduceAsync(_kafka.OrderCreatedTopic, new Message<string, string>
        {
            Key   = orderId.ToString(),
            Value = payload
        });

        using var consumer = _kafka.CreateConsumer($"key-test-{Guid.NewGuid():N}");
        consumer.Subscribe(_kafka.OrderCreatedTopic);
        var result = consumer.Consume(TimeSpan.FromSeconds(10));
        consumer.Close();

        result.Message.Key.Should().Be(orderId.ToString(),
            "Kafka message key must equal orderId for correct partition routing");
    }

    private record OrderCreatedDto(Guid OrderId, Guid UserId, string ProductName, decimal Amount, DateTime CreatedAt);
}
