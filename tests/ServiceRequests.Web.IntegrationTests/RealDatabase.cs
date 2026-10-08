using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ServiceRequests.Web.IntegrationTests;

/// <summary>
/// A throwaway PostgreSQL 17 container started by Testcontainers for these
/// tests only. It never touches the Compose database, its volume or Neon.
/// Docker must be running; without it the tests fail instead of passing.
/// </summary>
public sealed class PostgresContainer : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task StopAsync() => _container.StopAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<long> CountRowsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM service_requests", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}

/// <summary>The real app (real store, real schema step) pointed at the test container.</summary>
public sealed class RealDatabaseApp(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Postgres", connectionString);
}

internal static class Requests
{
    // Fictional data; example.com is a reserved domain.
    public const string ValidJson = """
        {"name":"  Deniz Örnek  ","email":" deniz.ornek@example.com ","service":"api-integration","description":"  Kurgusal entegrasyon testi: gerçek PostgreSQL.  "}
        """;

    public const string InvalidJson = """
        {"name":"D","email":"deniz@example","service":"consulting","description":"kısa"}
        """;

    public static Task<HttpResponseMessage> PostAsync(HttpClient client, string json) =>
        client.PostAsync("/api/requests", new StringContent(json, Encoding.UTF8, "application/json"));
}
