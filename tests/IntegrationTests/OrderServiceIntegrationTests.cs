using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Data;
using OrderService.Producers;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Integration tests for OrderService.
///
/// The Kafka producer is replaced with a no-op stub so that these tests focus on
/// the HTTP + database layer without requiring a running Kafka broker.
/// Event-driven behaviour is covered separately in EventTests.
/// </summary>
[Collection("SqlServer")]
public class OrderServiceIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<global::OrderService.Program> _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public OrderServiceIntegrationTests(SqlServerFixture sqlFixture)
    {
        _factory = new WebApplicationFactory<global::OrderService.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace SQL Server with Testcontainers instance
                    var dbDescriptor = services.SingleOrDefault(d =>
                        d.ServiceType == typeof(DbContextOptions<OrderDbContext>));
                    if (dbDescriptor is not null) services.Remove(dbDescriptor);
                    services.AddDbContext<OrderDbContext>(opt =>
                        opt.UseSqlServer(sqlFixture.ConnectionString));

                    // Replace Kafka producer with a no-op so tests don't require Kafka
                    var producerDescriptor = services.SingleOrDefault(d =>
                        d.ServiceType == typeof(IOrderEventProducer));
                    if (producerDescriptor is not null) services.Remove(producerDescriptor);
                    services.AddSingleton<IOrderEventProducer, NoOpOrderEventProducer>();
                });
            });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_PersistsToDatabase_AndReturns201()
    {
        var userId = Guid.NewGuid();
        var request = new { userId, productName = "Laptop", amount = 1299.99m };

        var response = await _client.PostAsJsonAsync("orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOpts);
        order!.UserId.Should().Be(userId);
        order.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task CreateOrder_ThenGetById_ReturnsCorrectOrder()
    {
        var userId = Guid.NewGuid();
        var createResponse = await _client.PostAsJsonAsync("orders",
            new { userId, productName = "Mechanical Keyboard", amount = 159.00m });
        var created = await createResponse.Content.ReadFromJsonAsync<OrderDto>(JsonOpts);

        var getResponse = await _client.GetAsync($"orders/{created!.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<OrderDto>(JsonOpts);
        fetched!.Id.Should().Be(created.Id);
        fetched.ProductName.Should().Be("Mechanical Keyboard");
    }

    [Fact]
    public async Task GetOrdersByUserId_ReturnsOnlyMatchingOrders()
    {
        var targetUserId = Guid.NewGuid();
        var otherUserId  = Guid.NewGuid();

        await _client.PostAsJsonAsync("orders", new { userId = targetUserId, productName = "Mouse", amount = 39.99m });
        await _client.PostAsJsonAsync("orders", new { userId = otherUserId,  productName = "Pad",   amount = 15.00m });

        var response = await _client.GetAsync($"orders?userId={targetUserId}");
        var orders   = await response.Content.ReadFromJsonAsync<OrderDto[]>(JsonOpts);

        orders.Should().NotBeNullOrEmpty();
        orders!.Should().OnlyContain(o => o.UserId == targetUserId);
    }

    [Fact]
    public async Task CreateOrder_ZeroAmount_Returns400()
    {
        var response = await _client.PostAsJsonAsync("orders",
            new { userId = Guid.NewGuid(), productName = "Free Item", amount = 0m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private record OrderDto(Guid Id, Guid UserId, string ProductName, decimal Amount, string Status, DateTime CreatedAt);
}

/// <summary>No-op Kafka producer stub used in integration tests.</summary>
internal class NoOpOrderEventProducer : IOrderEventProducer
{
    public Task PublishOrderCreatedAsync(OrderService.Events.OrderCreatedEvent evt, CancellationToken ct = default)
        => Task.CompletedTask;
}
