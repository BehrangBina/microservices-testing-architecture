using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace IntegrationTests.Fixtures;

/// <summary>
/// Spins up a real SQL Server container via Testcontainers.
/// Shared across all tests in a collection via ICollectionFixture.
///
/// Lifecycle:
///   InitializeAsync  — container start
///   DisposeAsync     — container stop + remove
///
/// The ConnectionString property is available to WebApplicationFactory overrides.
/// </summary>
public class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Your_password123!")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Verify connectivity
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

/// <summary>
/// xUnit collection definition — ensures the SQL Server container is shared
/// across all test classes in the [Collection("SqlServer")] group.
/// </summary>
[CollectionDefinition("SqlServer")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture> { }
