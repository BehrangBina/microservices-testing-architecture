using System.Net;
using System.Text.Json;
using FluentAssertions;
using RestSharp;
using Xunit;

namespace ApiTests;

/// <summary>
/// API-level tests for NotificationService.
/// Notifications are created by Kafka consumers — these tests validate query endpoints.
/// </summary>
public class NotificationServiceTests : ApiTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

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
    public async Task GetNotificationById_NonExistent_Returns404()
    {
        var response = await NotifyClient.ExecuteAsync(
            new RestRequest($"notifications/{Guid.NewGuid()}"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private record NotificationDto(Guid Id, string EventType, Guid? OrderId, string Payload, DateTime ReceivedAt);
}
