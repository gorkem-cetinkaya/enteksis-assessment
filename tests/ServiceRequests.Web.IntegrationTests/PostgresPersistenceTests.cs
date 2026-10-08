using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace ServiceRequests.Web.IntegrationTests;

/// <summary>The real Npgsql store and schema step against a real PostgreSQL.</summary>
public class PostgresPersistenceTests(PostgresContainer database) : IClassFixture<PostgresContainer>
{
    [Fact]
    public async Task Valid_post_returns_201_and_the_trimmed_row_can_be_read_over_another_connection()
    {
        await using var app = new RealDatabaseApp(database.ConnectionString);
        using var client = app.CreateClient();

        var response = await Requests.PostAsync(client, Requests.ValidJson);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var requestId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT name, email, service, description, created_at FROM service_requests WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", requestId);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync(), $"No row with id {requestId}.");
        Assert.Equal("Deniz Örnek", reader.GetString(0));
        Assert.Equal("deniz.ornek@example.com", reader.GetString(1));
        Assert.Equal("api-integration", reader.GetString(2));
        Assert.Equal("Kurgusal entegrasyon testi: gerçek PostgreSQL.", reader.GetString(3));
        var createdAt = reader.GetFieldValue<DateTime>(4);
        Assert.Equal(DateTimeKind.Utc, createdAt.Kind);
        Assert.InRange(createdAt, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Invalid_post_returns_400_and_stores_no_row()
    {
        await using var app = new RealDatabaseApp(database.ConnectionString);
        using var client = app.CreateClient();
        await client.GetAsync("/health"); // start the host so the schema exists before counting
        var before = await database.CountRowsAsync();

        var response = await Requests.PostAsync(client, Requests.InvalidJson);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await database.CountRowsAsync());
    }

    [Fact]
    public async Task A_new_app_host_on_the_same_running_database_keeps_earlier_rows()
    {
        Guid requestId;
        await using (var first = new RealDatabaseApp(database.ConnectionString))
        {
            using var client = first.CreateClient();
            var response = await Requests.PostAsync(client, Requests.ValidJson);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            requestId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid();
        }

        var rowsBefore = await database.CountRowsAsync();

        // Starting a second host runs Data/schema.sql again on the same database.
        await using (var second = new RealDatabaseApp(database.ConnectionString))
        {
            using var client = second.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        }

        Assert.Equal(rowsBefore, await database.CountRowsAsync());
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM service_requests WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", requestId);
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }
}
