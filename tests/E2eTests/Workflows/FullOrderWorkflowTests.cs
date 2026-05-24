using System.Text.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace E2eTests.Workflows;

/// <summary>
/// End-to-end workflow tests for the full order lifecycle.
///
/// Prerequisites — all services must be running (docker compose up):
///   user-service      → http://localhost:5001
///   order-service     → http://localhost:5002
///   payment-service   → http://localhost:5003
///   notification-service → http://localhost:5004
///
/// Workflow under test:
///   1. Create a user via UserService
///   2. Create an order via OrderService  → publishes OrderCreated to Kafka
///   3. Poll PaymentService until a Payment is found (PaymentService consumed the event)
///   4. Poll NotificationService until an OrderCreated notification is found
///   5. Poll NotificationService until a PaymentProcessed notification is found
/// </summary>
public class FullOrderWorkflowTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private PlaywrightApiContext _api = null!;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public FullOrderWorkflowTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => _api = await PlaywrightApiContext.CreateAsync();
    public async Task DisposeAsync()    => await _api.DisposeAsync();

    [Fact]
    public async Task FullOrderWorkflow_CreatesPaymentAndNotifications_EndToEnd()
    {
        // ── Step 1: Create a user ───────────────────────────────────────────────
        _output.WriteLine("Step 1: Creating user...");
        var userResponse = await _api.PostAsync($"{_api.UserServiceUrl}/users", new
        {
            name  = $"E2E User {Guid.NewGuid():N}",
            email = $"e2e+{Guid.NewGuid():N}@test.com"
        });

        userResponse.Status.Should().Be(201, "user creation must return 201 Created");
        var user = JsonSerializer.Deserialize<UserDto>(await userResponse.TextAsync(), JsonOpts)!;
        _output.WriteLine($"  Created user: {user.Id}");

        // ── Step 2: Create an order ─────────────────────────────────────────────
        _output.WriteLine("Step 2: Creating order...");
        var orderResponse = await _api.PostAsync($"{_api.OrderServiceUrl}/orders", new
        {
            userId      = user.Id,
            productName = "Ergonomic Chair",
            amount      = 449.00m
        });

        orderResponse.Status.Should().Be(201, "order creation must return 201 Created");
        var order = JsonSerializer.Deserialize<OrderDto>(await orderResponse.TextAsync(), JsonOpts)!;
        _output.WriteLine($"  Created order: {order.Id}");

        // ── Step 3: Poll PaymentService — async event consumer ──────────────────
        _output.WriteLine("Step 3: Waiting for PaymentService to process OrderCreated event...");
        var payment = await PollingHelper.WaitForAsync(
            condition: async () =>
            {
                var resp = await _api.GetAsync($"{_api.PaymentServiceUrl}/payments/order/{order.Id}");
                if (resp.Status != 200) return null;
                return JsonSerializer.Deserialize<PaymentDto>(await resp.TextAsync(), JsonOpts);
            },
            timeout:     TimeSpan.FromSeconds(15),
            interval:    TimeSpan.FromMilliseconds(500),
            failMessage: $"PaymentService did not create a payment for OrderId {order.Id} within 15s");

        payment.OrderId.Should().Be(order.Id);
        payment.Status.Should().Be("Processed");
        _output.WriteLine($"  Payment received: {payment.Id}, status: {payment.Status}");

        // ── Step 4: Poll NotificationService — OrderCreated notification ─────────
        _output.WriteLine("Step 4: Waiting for OrderCreated notification...");
        await PollingHelper.WaitForTrueAsync(
            condition: async () =>
            {
                var resp = await _api.GetAsync(
                    $"{_api.NotifyServiceUrl}/notifications?orderId={order.Id}&eventType=OrderCreated");
                if (resp.Status != 200) return false;
                var notifications = JsonSerializer.Deserialize<NotificationDto[]>(await resp.TextAsync(), JsonOpts)!;
                return notifications.Length > 0;
            },
            timeout:     TimeSpan.FromSeconds(15),
            failMessage: $"NotificationService did not log OrderCreated for OrderId {order.Id} within 15s");

        _output.WriteLine("  OrderCreated notification confirmed.");

        // ── Step 5: Poll NotificationService — PaymentProcessed notification ────
        _output.WriteLine("Step 5: Waiting for PaymentProcessed notification...");
        await PollingHelper.WaitForTrueAsync(
            condition: async () =>
            {
                var resp = await _api.GetAsync(
                    $"{_api.NotifyServiceUrl}/notifications?orderId={order.Id}&eventType=PaymentProcessed");
                if (resp.Status != 200) return false;
                var notifications = JsonSerializer.Deserialize<NotificationDto[]>(await resp.TextAsync(), JsonOpts)!;
                return notifications.Length > 0;
            },
            timeout:     TimeSpan.FromSeconds(15),
            failMessage: $"NotificationService did not log PaymentProcessed for OrderId {order.Id} within 15s");

        _output.WriteLine("  PaymentProcessed notification confirmed.");
        _output.WriteLine("Full order workflow completed successfully.");
    }

    [Fact]
    public async Task CreateOrder_WithInvalidUserId_StillAcceptedByOrderService()
    {
        // OrderService doesn't validate user existence (that's the contract test's job).
        // This confirms the API layer accepts any valid UUID.
        var response = await _api.PostAsync($"{_api.OrderServiceUrl}/orders", new
        {
            userId      = Guid.NewGuid(),
            productName = "Unknown User Product",
            amount      = 19.99m
        });

        response.Status.Should().Be(201,
            "OrderService accepts any userId — user validation is a separate concern");
    }

    private record UserDto(Guid Id, string Name, string Email, DateTime CreatedAt);
    private record OrderDto(Guid Id, Guid UserId, string ProductName, decimal Amount, string Status, DateTime CreatedAt);
    private record PaymentDto(Guid Id, Guid OrderId, decimal Amount, string Status, DateTime ProcessedAt);
    private record NotificationDto(Guid Id, string EventType, Guid? OrderId, string Payload, DateTime ReceivedAt);
}
