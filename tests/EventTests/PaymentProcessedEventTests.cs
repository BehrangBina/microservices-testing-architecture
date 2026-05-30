using Confluent.Kafka;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace EventTests;

/// <summary>
/// Tests for the PaymentProcessed event — produce, consume, and validate status enum values.
/// </summary>
public class PaymentProcessedEventTests : IClassFixture<KafkaTestFixture>
{
    private readonly KafkaTestFixture _kafka;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public PaymentProcessedEventTests(KafkaTestFixture kafka) => _kafka = kafka;

    [Fact]
    public async Task PaymentProcessedEvent_ProducedAndConsumed_FieldsAreValid()
    {
        var evt = new
        {
            paymentId = Guid.NewGuid(),
            orderId = Guid.NewGuid(),
            amount = 149.99m,
            status = "Processed",
            processedAt = DateTime.UtcNow
        };

        using var producer = _kafka.CreateProducer();
        using var consumer = _kafka.CreateConsumer($"payment-group-{Guid.NewGuid():N}");
        consumer.Subscribe(_kafka.PaymentProcessedTopic);

        await producer.ProduceAsync(_kafka.PaymentProcessedTopic, new Message<string, string>
        {
            Key = evt.orderId.ToString(),
            Value = JsonSerializer.Serialize(evt, JsonOpts)
        });

        var result = ConsumeByKey(consumer, evt.orderId.ToString(), TimeSpan.FromSeconds(10));
        consumer.Close();

        result.Should().NotBeNull();
        var consumed = JsonSerializer.Deserialize<PaymentProcessedDto>(result.Message.Value, JsonOpts)!;
        consumed.PaymentId.Should().NotBe(Guid.Empty);
        consumed.OrderId.Should().Be(evt.orderId);
        consumed.Amount.Should().BeGreaterThan(0);
        consumed.Status.Should().BeOneOf("Processed", "Failed", "Refunded");
    }

    [Theory]
    [InlineData("Processed")]
    [InlineData("Failed")]
    [InlineData("Refunded")]
    public async Task PaymentProcessedEvent_AllValidStatuses_AreAccepted(string status)
    {
        var orderId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            paymentId = Guid.NewGuid(),
            orderId,
            amount = 10.00m,
            status,
            processedAt = DateTime.UtcNow
        }, JsonOpts);

        using var producer = _kafka.CreateProducer();
        await producer.ProduceAsync(_kafka.PaymentProcessedTopic, new Message<string, string>
        {
            Key = orderId.ToString(),
            Value = payload
        });

        using var consumer = _kafka.CreateConsumer($"status-test-{Guid.NewGuid():N}");
        consumer.Subscribe(_kafka.PaymentProcessedTopic);
        var result = ConsumeByKey(consumer, orderId.ToString(), TimeSpan.FromSeconds(10));
        consumer.Close();

        var consumed = JsonSerializer.Deserialize<PaymentProcessedDto>(result.Message.Value, JsonOpts)!;
        consumed.Status.Should().Be(status);
    }

    private static ConsumeResult<string, string> ConsumeByKey(
        IConsumer<string, string> consumer,
        string expectedKey,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var result = consumer.Consume(remaining);
            if (result is not null && result.Message.Key == expectedKey)
            {
                return result;
            }
        }

        throw new Xunit.Sdk.XunitException($"Did not consume message with expected key '{expectedKey}' within {timeout.TotalSeconds:0}s.");
    }

    private record PaymentProcessedDto(Guid PaymentId, Guid OrderId, decimal Amount, string Status, DateTime ProcessedAt);
}
