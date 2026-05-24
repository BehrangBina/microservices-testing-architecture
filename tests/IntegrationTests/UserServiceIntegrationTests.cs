using System.Net;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UserService.Data;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Integration tests for UserService against a real SQL Server (via Testcontainers).
///
/// Uses WebApplicationFactory to spin up UserService in-process with the connection
/// string overridden to point to the containerised SQL Server instance.
/// </summary>
[Collection("SqlServer")]
public class UserServiceIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public UserServiceIntegrationTests(SqlServerFixture sqlFixture)
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace SQL Server registration with the Testcontainers connection string
                    var descriptor = services.SingleOrDefault(d =>
                        d.ServiceType == typeof(DbContextOptions<UserDbContext>));
                    if (descriptor is not null) services.Remove(descriptor);

                    services.AddDbContext<UserDbContext>(opt =>
                        opt.UseSqlServer(sqlFixture.ConnectionString));
                });
            });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateUser_PersistsToDatabase_AndReturns201()
    {
        var request = new { name = "Integration Alice", email = $"ialice+{Guid.NewGuid()}@test.com" };

        var response = await _client.PostAsJsonAsync("users", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await response.Content.ReadFromJsonAsync<UserDto>(JsonOpts);
        user!.Id.Should().NotBe(Guid.Empty);
        user.Name.Should().Be("Integration Alice");
    }

    [Fact]
    public async Task CreateAndRetrieveUser_FullRoundTrip()
    {
        // Create
        var createResponse = await _client.PostAsJsonAsync("users",
            new { name = "Bob Integration", email = $"bobint+{Guid.NewGuid()}@test.com" });
        var created = await createResponse.Content.ReadFromJsonAsync<UserDto>(JsonOpts);

        // Retrieve
        var getResponse = await _client.GetAsync($"users/{created!.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<UserDto>(JsonOpts);
        fetched!.Id.Should().Be(created.Id);
        fetched.Email.Should().Be(created.Email);
    }

    [Fact]
    public async Task DeleteUser_RemovesFromDatabase()
    {
        // Seed
        var createResponse = await _client.PostAsJsonAsync("users",
            new { name = "Delete Me", email = $"delme+{Guid.NewGuid()}@test.com" });
        var user = await createResponse.Content.ReadFromJsonAsync<UserDto>(JsonOpts);

        // Delete
        var deleteResponse = await _client.DeleteAsync($"users/{user!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Confirm gone
        var getResponse = await _client.GetAsync($"users/{user.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllUsers_AfterSeeding_ReturnsPopulatedList()
    {
        var email1 = $"list1+{Guid.NewGuid()}@test.com";
        var email2 = $"list2+{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("users", new { name = "List User 1", email = email1 });
        await _client.PostAsJsonAsync("users", new { name = "List User 2", email = email2 });

        var response = await _client.GetAsync("users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<UserDto[]>(JsonOpts);
        users.Should().NotBeNullOrEmpty();
        users!.Select(u => u.Email).Should().Contain(email1).And.Contain(email2);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private record UserDto(Guid Id, string Name, string Email, DateTime CreatedAt);
}
