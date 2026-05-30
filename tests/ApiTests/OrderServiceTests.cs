using FluentAssertions;
using RestSharp;
using System.Net;
using System.Text.Json;
using Xunit;

namespace ApiTests;

/// <summary>
/// API-level tests for OrderService.
/// Requires: order-service and user-service running.
/// </summary>
public class OrderServiceTests : ApiTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private async Task<Guid> CreateUserAsync()
    {
        var request = new RestRequest("users", Method.Post);
        request.AddJsonBody(new { name = "Order User", email = $"order+{Guid.NewGuid()}@test.com" });
        var response = await UserClient.ExecuteAsync(request);
        var user = JsonSerializer.Deserialize<UserDto>(response.Content!, JsonOpts)!;
        return user.Id;
    }

    [Fact]
    public async Task CreateOrder_ValidPayload_Returns201()
    {
        var userId = await CreateUserAsync();

        var request = new RestRequest("orders", Method.Post);
        request.AddJsonBody(new { userId, productName = "Laptop Pro", amount = 1299.99m });

        var response = await OrderClient.ExecuteAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = JsonSerializer.Deserialize<OrderDto>(response.Content!, JsonOpts);
        order!.Id.Should().NotBe(Guid.Empty);
        order.UserId.Should().Be(userId);
        order.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task CreateOrder_MissingFields_Returns400()
    {
        var request = new RestRequest("orders", Method.Post);
        request.AddJsonBody(new { productName = "Ghost Order" }); // missing userId and amount

        var response = await OrderClient.ExecuteAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOrderById_ExistingOrder_ReturnsOrder()
    {
        var userId = await CreateUserAsync();
        var createRequest = new RestRequest("orders", Method.Post);
        createRequest.AddJsonBody(new { userId, productName = "Keyboard", amount = 199.99m });
        var created = await OrderClient.ExecuteAsync(createRequest);
        var order = JsonSerializer.Deserialize<OrderDto>(created.Content!, JsonOpts)!;

        var response = await OrderClient.ExecuteAsync(new RestRequest($"orders/{order.Id}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonSerializer.Deserialize<OrderDto>(response.Content!, JsonOpts)!.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task GetOrderById_NonExistent_Returns404()
    {
        var response = await OrderClient.ExecuteAsync(new RestRequest($"orders/{Guid.NewGuid()}"));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOrdersByUserId_ReturnsFilteredOrders()
    {
        var userId = await CreateUserAsync();
        var createRequest = new RestRequest("orders", Method.Post);
        createRequest.AddJsonBody(new { userId, productName = "Monitor", amount = 399.99m });
        await OrderClient.ExecuteAsync(createRequest);

        var response = await OrderClient.ExecuteAsync(new RestRequest($"orders?userId={userId}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = JsonSerializer.Deserialize<OrderDto[]>(response.Content!, JsonOpts)!;
        orders.Should().NotBeEmpty();
        orders.Should().OnlyContain(o => o.UserId == userId);
    }

    private record UserDto(Guid Id, string Name, string Email, DateTime CreatedAt);
    private record OrderDto(Guid Id, Guid UserId, string ProductName, decimal Amount, string Status, DateTime CreatedAt);
}
