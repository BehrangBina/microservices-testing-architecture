# Event-Driven Testing Guide

## Why Event-Driven Testing Is Different

In a synchronous REST system, testing is straightforward: call the API, assert the response. In an event-driven system, **cause and effect are decoupled** — an HTTP call produces a Kafka event, and a different service consumes it asynchronously. This requires a different testing vocabulary.

---

## Event Flow in This System

```
OrderService ──POST /orders──► DB
                 │
                 └──► Kafka: order-created ──► PaymentService consumer
                                                    │
                                                    ├──► DB (Payment record)
                                                    └──► Kafka: payment-processed ──► NotificationService consumer
                                                                                           │
                                                                                           └──► DB (Notification log)
```

---

## Kafka Topics

| Topic | Producer | Consumer(s) | Schema file |
|---|---|---|---|
| `order-created` | OrderService | PaymentService, NotificationService | `tests/EventTests/Schemas/order-created.schema.json` |
| `payment-processed` | PaymentService | NotificationService | `tests/EventTests/Schemas/payment-processed.schema.json` |

---

## Testing Patterns

### 1. Produce → Consume Assertion

Verifies that a produced message can be consumed and that fields are correctly typed.

```
Producer ──► [topic] ──► Consumer ──► Assert fields
```

**Key principle:** Use a unique topic prefix per test run (`test-{guid}`) to avoid cross-contamination between parallel test executions.

```csharp
// KafkaTestFixture.cs — isolation pattern
public string OrderCreatedTopic => $"{TopicPrefix}-order-created";
// TopicPrefix = $"test-{Guid.NewGuid():N}"
```

### 2. Schema Validation

Validates that a JSON message conforms to its schema definition. **Runs without Kafka** — pure structural assertion.

```csharp
var schema = await JsonSchema.FromFileAsync("Schemas/order-created.schema.json");
var errors = schema.Validate(JsonSerializer.Serialize(evt));
errors.Should().BeEmpty();
```

**Why this matters:** Schema drift is one of the most common causes of event-driven system failures. A consumer breaks because a required field was renamed or its type changed. Schema validation tests catch this at build time.

### 3. Idempotency Validation

PaymentService implements idempotent consumption — if the same `OrderCreated` event is replayed, only one payment is created.

```csharp
// In PaymentService/Consumers/OrderCreatedConsumer.cs
if (await db.Payments.AnyAsync(p => p.OrderId == evt.OrderId, ct))
{
    _logger.LogWarning("Payment already exists for OrderId {OrderId}, skipping", evt.OrderId);
    return;
}
```

Integration tests validate this by publishing the same event twice and asserting only one payment record exists.

### 4. Eventual Consistency Assertions (E2E)

E2E tests use `PollingHelper` to assert outcomes that happen asynchronously:

```csharp
var payment = await PollingHelper.WaitForAsync(
    condition: async () => {
        var resp = await _api.GetAsync($"{PaymentServiceUrl}/payments/order/{orderId}");
        return resp.Status == 200
            ? JsonSerializer.Deserialize<PaymentDto>(await resp.TextAsync())
            : null;
    },
    timeout: TimeSpan.FromSeconds(15),
    failMessage: $"PaymentService did not process order {orderId} within 15s"
);
```

---

## Consumer Group Isolation

Each service uses a **unique consumer group ID** to ensure independent progress tracking:

| Service | Consumer class | Group ID |
|---|---|---|
| PaymentService | OrderCreatedConsumer | `payment-service-group` |
| NotificationService | OrderCreatedConsumer | `notification-service-order-group` |
| NotificationService | PaymentProcessedConsumer | `notification-service-payment-group` |

This ensures both PaymentService and NotificationService each receive every `order-created` message, even if one is temporarily down.

---

## Running Event Tests

```bash
# Requires Kafka running on localhost:9094 (exposed port from docker-compose)
docker compose -f infra/docker-compose.yml up -d kafka kafka-init

# Run all event tests
dotnet test tests/EventTests/EventTests.csproj

# Run only schema validation (no Kafka required)
dotnet test tests/EventTests/EventTests.csproj \
  --filter "FullyQualifiedName~SchemaValidation"
```

---

## Extending Event Tests

### Adding a new event

1. Define the JSON Schema in `tests/EventTests/Schemas/{event-name}.schema.json`
2. Add a schema validation test in `SchemaValidationTests.cs`
3. Add a produce/consume test in a new `{EventName}Tests.cs` file
4. Register the new topic in `KafkaTestFixture.cs`
5. Add the topic to `infra/kafka/create-topics.sh`

### Adding a new consumer

Add a consumer group isolation test that publishes N messages and asserts all N are received by **each** consumer group independently. This confirms the fan-out behaviour is correct.

---

## Anti-Patterns to Avoid

| Anti-pattern | Risk | Solution |
|---|---|---|
| Shared Kafka topics between test runs | Tests interfere with each other | Use `test-{guid}` topic prefixes |
| No schema validation | Schema drift breaks consumers silently | Always add a schema test when adding an event |
| Polling without timeout | Test hangs forever | Always use `PollingHelper` with a bounded timeout |
| Testing consumers against production Kafka | Tests affect live data | Use dedicated test broker or topic isolation |
