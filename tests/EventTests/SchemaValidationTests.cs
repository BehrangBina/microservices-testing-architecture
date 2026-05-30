using FluentAssertions;
using NJsonSchema;
using System.Text.Json;
using Xunit;

namespace EventTests;

/// <summary>
/// Schema validation tests.
///
/// Validates that event payloads conform to the JSON Schema definitions in
/// tests/EventTests/Schemas/. This catches schema drift early — before a consumer
/// breaks because a required field was removed or a type was changed.
///
/// These tests run without Kafka — they are pure schema assertions.
/// </summary>
public class SchemaValidationTests
{
    private static readonly string SchemaDir = Path.Combine(
        AppContext.BaseDirectory, "Schemas");

    [Fact]
    public async Task OrderCreatedEvent_ValidPayload_PassesSchemaValidation()
    {
        var schema = await JsonSchema.FromFileAsync(
            Path.Combine(SchemaDir, "order-created.schema.json"));

        var payload = new
        {
            orderId = Guid.NewGuid(),
            userId = Guid.NewGuid(),
            productName = "Gaming Mouse",
            amount = 59.99,
            createdAt = "2024-06-01T12:00:00Z"
        };

        var errors = schema.Validate(JsonSerializer.Serialize(payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        errors.Should().BeEmpty("valid OrderCreatedEvent should pass schema validation");
    }

    [Fact]
    public async Task OrderCreatedEvent_MissingRequiredField_FailsValidation()
    {
        var schema = await JsonSchema.FromFileAsync(
            Path.Combine(SchemaDir, "order-created.schema.json"));

        // Missing 'amount' — a required field
        var invalid = new
        {
            orderId = Guid.NewGuid(),
            userId = Guid.NewGuid(),
            productName = "Headset",
            createdAt = "2024-06-01T12:00:00Z"
        };

        var errors = schema.Validate(JsonSerializer.Serialize(invalid,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        errors.Should().NotBeEmpty("missing 'amount' must trigger a schema violation");
    }

    [Fact]
    public async Task OrderCreatedEvent_InvalidAmountType_FailsValidation()
    {
        var schema = await JsonSchema.FromFileAsync(
            Path.Combine(SchemaDir, "order-created.schema.json"));

        // 'amount' is a string instead of a number
        var json = """
            {
              "orderId":     "11111111-1111-1111-1111-111111111111",
              "userId":      "22222222-2222-2222-2222-222222222222",
              "productName": "Webcam",
              "amount":      "not-a-number",
              "createdAt":   "2024-06-01T12:00:00Z"
            }
            """;

        var errors = schema.Validate(json);
        errors.Should().NotBeEmpty("'amount' must be a number");
    }

    [Fact]
    public async Task PaymentProcessedEvent_ValidPayload_PassesSchemaValidation()
    {
        var schema = await JsonSchema.FromFileAsync(
            Path.Combine(SchemaDir, "payment-processed.schema.json"));

        var payload = new
        {
            paymentId = Guid.NewGuid(),
            orderId = Guid.NewGuid(),
            amount = 149.99,
            status = "Processed",
            processedAt = "2024-06-01T12:05:00Z"
        };

        var errors = schema.Validate(JsonSerializer.Serialize(payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        errors.Should().BeEmpty("valid PaymentProcessedEvent should pass schema validation");
    }

    [Fact]
    public async Task PaymentProcessedEvent_InvalidStatus_FailsValidation()
    {
        var schema = await JsonSchema.FromFileAsync(
            Path.Combine(SchemaDir, "payment-processed.schema.json"));

        // 'status' value not in allowed enum
        var json = """
            {
              "paymentId":   "33333333-3333-3333-3333-333333333333",
              "orderId":     "44444444-4444-4444-4444-444444444444",
              "amount":      49.99,
              "status":      "Pending",
              "processedAt": "2024-06-01T12:05:00Z"
            }
            """;

        var errors = schema.Validate(json);
        errors.Should().NotBeEmpty("'Pending' is not a valid status enum value");
    }
}
