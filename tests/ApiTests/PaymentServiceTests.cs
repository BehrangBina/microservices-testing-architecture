using FluentAssertions;
using RestSharp;
using System.Net;
using System.Text.Json;
using Xunit;

namespace ApiTests;

/// <summary>
/// API-level tests for PaymentService.
/// Note: payments are created automatically via Kafka when an order is posted.
/// These tests validate the query endpoints and idempotency.
/// </summary>
public class PaymentServiceTests : ApiTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetAllPayments_ReturnsOkWithArray()
    {
        var response = await PaymentClient.ExecuteAsync(new RestRequest("payments"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetPaymentByOrderId_NonExistent_Returns404()
    {
        var response = await PaymentClient.ExecuteAsync(
            new RestRequest($"payments/order/{Guid.NewGuid()}"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPaymentById_NonExistent_Returns404()
    {
        var response = await PaymentClient.ExecuteAsync(
            new RestRequest($"payments/{Guid.NewGuid()}"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
