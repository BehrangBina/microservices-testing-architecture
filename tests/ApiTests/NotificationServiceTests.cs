using FluentAssertions;
using RestSharp;
using System.Net;
using System.Text.Json;
using Xunit;

namespace ApiTests;

/// <summary>
/// API-level tests for NotificationService.
/// Notifications are created by Kafka consumers — these tests validate query endpoints.
/// </summary>
public class NotificationServiceTests : ApiTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private async Task<Guid> CreateOrderAsync()
    {
        var createUser = new RestRequest("users", Method.Post);
        createUser.AddJsonBody(new { name = "Notify User", email = $"notify+{Guid.NewGuid()}@test.com" });
        var userResponse = await UserClient.ExecuteAsync(createUser);
        userResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = JsonSerializer.Deserialize<UserDto>(userResponse.Content!, JsonOpts)!;

        var createOrder = new RestRequest("orders", Method.Post);
        createOrder.AddJsonBody(new { userId = user.Id, productName = "Coverage Item", amount = 49.99m });
        var orderResponse = await OrderClient.ExecuteAsync(createOrder);
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = JsonSerializer.Deserialize<OrderDto>(orderResponse.Content!, JsonOpts)!;

        return order.Id;
    }

    private async Task<NotificationDto[]?> WaitForNotificationsAsync(Guid orderId, string? eventType = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var route = eventType is null
                ? $"notifications?orderId={orderId}"
                : $"notifications?orderId={orderId}&eventType={eventType}";

            var response = await NotifyClient.ExecuteAsync(new RestRequest(route));
            if (response.StatusCode == HttpStatusCode.OK && !string.IsNullOrWhiteSpace(response.Content))
            {
                var notifications = JsonSerializer.Deserialize<NotificationDto[]>(response.Content, JsonOpts);
                if (notifications is { Length: > 0 })
                    return notifications;
            }

            await Task.Delay(500);
        }

        return null;
    }

    [Fact]
    public async Task GetAllNotifications_ReturnsOkWithArray()
    {
        var response = await NotifyClient.ExecuteAsync(new RestRequest("notifications"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetNotifications_FilterByEventType_ReturnsFilteredResults()
    {
        var response = await NotifyClient.ExecuteAsync(
            new RestRequest("notifications?eventType=OrderCreated"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var notifications = JsonSerializer.Deserialize<NotificationDto[]>(response.Content!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        notifications.Should().OnlyContain(n => n.EventType == "OrderCreated");
    }

    [Fact]
    public async Task GetNotifications_FilterByOrderId_ReturnsFilteredResults()
    {
        var orderId = await CreateOrderAsync();
        var notifications = await WaitForNotificationsAsync(orderId);

        notifications.Should().NotBeNullOrEmpty();
        notifications!.Should().OnlyContain(n => n.OrderId == orderId);
    }

    [Fact]
    public async Task GetNotifications_FilterByOrderIdAndEventType_ReturnsFilteredResults()
    {
        var orderId = await CreateOrderAsync();
        var notifications = await WaitForNotificationsAsync(orderId, "OrderCreated");

        notifications.Should().NotBeNullOrEmpty();
        notifications!.Should().OnlyContain(n => n.OrderId == orderId && n.EventType == "OrderCreated");
    }

    [Fact]
    public async Task GetNotificationById_Existing_ReturnsNotification()
    {
        var orderId = await CreateOrderAsync();
        var notifications = await WaitForNotificationsAsync(orderId, "OrderCreated");
        notifications.Should().NotBeNullOrEmpty();

        var notification = notifications![0];
        var response = await NotifyClient.ExecuteAsync(new RestRequest($"notifications/{notification.Id}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = JsonSerializer.Deserialize<NotificationDto>(response.Content!, JsonOpts);
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(notification.Id);
    }

    [Fact]
    public async Task GetNotificationById_NonExistent_Returns404()
    {
        var response = await NotifyClient.ExecuteAsync(
            new RestRequest($"notifications/{Guid.NewGuid()}"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private record UserDto(Guid Id, string Name, string Email, DateTime CreatedAt);
    private record OrderDto(Guid Id, Guid UserId, string ProductName, decimal Amount, string Status, DateTime CreatedAt);
    private record NotificationDto(Guid Id, string EventType, Guid? OrderId, string Payload, DateTime ReceivedAt);
}
