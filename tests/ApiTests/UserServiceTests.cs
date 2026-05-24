using System.Net;
using System.Text.Json;
using FluentAssertions;
using RestSharp;
using Xunit;

namespace ApiTests;

/// <summary>
/// API-level tests for UserService.
/// Requires: user-service running at USER_SERVICE_URL (default http://localhost:5001).
/// </summary>
public class UserServiceTests : ApiTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetAllUsers_ReturnsOk()
    {
        var response = await UserClient.ExecuteAsync(new RestRequest("users"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateUser_ValidPayload_Returns201WithLocation()
    {
        var request = new RestRequest("users", Method.Post);
        request.AddJsonBody(new { name = "Alice Test", email = $"alice+{Guid.NewGuid()}@test.com" });

        var response = await UserClient.ExecuteAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = JsonSerializer.Deserialize<UserDto>(response.Content!, JsonOpts);
        user!.Id.Should().NotBe(Guid.Empty);
        user.Name.Should().Be("Alice Test");
    }

    [Fact]
    public async Task CreateUser_MissingName_Returns400()
    {
        var request = new RestRequest("users", Method.Post);
        request.AddJsonBody(new { email = "missing@name.com" });

        var response = await UserClient.ExecuteAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetUserById_ExistingUser_ReturnsUser()
    {
        // Arrange — create user first
        var createRequest = new RestRequest("users", Method.Post);
        createRequest.AddJsonBody(new { name = "Bob Test", email = $"bob+{Guid.NewGuid()}@test.com" });
        var created = await UserClient.ExecuteAsync(createRequest);
        var user = JsonSerializer.Deserialize<UserDto>(created.Content!, JsonOpts)!;

        // Act
        var response = await UserClient.ExecuteAsync(new RestRequest($"users/{user.Id}"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = JsonSerializer.Deserialize<UserDto>(response.Content!, JsonOpts);
        fetched!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetUserById_NonExistent_Returns404()
    {
        var response = await UserClient.ExecuteAsync(new RestRequest($"users/{Guid.NewGuid()}"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_ExistingUser_Returns204()
    {
        // Arrange
        var createRequest = new RestRequest("users", Method.Post);
        createRequest.AddJsonBody(new { name = "ToDelete", email = $"del+{Guid.NewGuid()}@test.com" });
        var created = await UserClient.ExecuteAsync(createRequest);
        var user = JsonSerializer.Deserialize<UserDto>(created.Content!, JsonOpts)!;

        // Act
        var response = await UserClient.ExecuteAsync(new RestRequest($"users/{user.Id}", Method.Delete));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private record UserDto(Guid Id, string Name, string Email, DateTime CreatedAt);
}
