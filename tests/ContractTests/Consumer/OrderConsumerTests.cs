using System.Net;
using System.Text.Json;
using PactNet;
using PactNet.Matchers;
using Xunit;
using Xunit.Abstractions;

namespace ContractTests.Consumer;

/// <summary>
/// Consumer-side Pact test.
///
/// Context: OrderService calls GET /users/{id} on UserService before creating an order
/// to verify the user exists. This test defines the expected contract.
///
/// Running this test generates: tests/ContractTests/pacts/order-service-user-service.json
/// That file is the living contract used by UserProviderTests to verify the provider.
/// </summary>
public class OrderConsumerTests
{
    private readonly IPactBuilderV4 _pact;
    private readonly string _pactDir;

    public OrderConsumerTests(ITestOutputHelper output)
    {
        _pactDir = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "pacts");  // tests/ContractTests/pacts/

        Directory.CreateDirectory(_pactDir);

        var config = new PactConfig
        {
            PactDir = _pactDir,
            LogLevel = PactLogLevel.Warn
        };

        _pact = Pact.V4("order-service", "user-service", config).WithHttpInteractions();
    }

    [Fact]
    public async Task GetUserById_WhenUserExists_Returns200WithUserDetails()
    {
        var userId = new Guid("11111111-1111-1111-1111-111111111111");

        _pact
            .UponReceiving("a request to get a user by id")
            .WithRequest(HttpMethod.Get, $"/users/{userId}")
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                id     = Match.Type(userId.ToString()),
                name   = Match.Type("Alice"),
                email  = Match.Regex("alice@example.com", @"^[^@]+@[^@]+\.[^@]+$"),
                createdAt = Match.Type("2024-01-01T00:00:00Z")
            });

        await _pact.VerifyAsync(async ctx =>
        {
            // Simulate what OrderService would do — call UserService to validate the user
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            var response = await client.GetAsync($"/users/{userId}");

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<UserDto>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.NotNull(user);
            Assert.Equal(userId.ToString(), user!.Id.ToString(), ignoreCase: true);
            Assert.False(string.IsNullOrEmpty(user.Name));
            Assert.Contains("@", user.Email);
        });
    }

    [Fact]
    public async Task GetUserById_WhenUserDoesNotExist_Returns404()
    {
        var unknownId = new Guid("99999999-9999-9999-9999-999999999999");

        _pact
            .UponReceiving("a request for a non-existent user")
            .WithRequest(HttpMethod.Get, $"/users/{unknownId}")
            .WillRespond()
            .WithStatus(HttpStatusCode.NotFound);

        await _pact.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            var response = await client.GetAsync($"/users/{unknownId}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    private record UserDto(Guid Id, string Name, string Email, DateTime CreatedAt);
}
