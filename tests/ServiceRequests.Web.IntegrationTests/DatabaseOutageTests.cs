using System.Net;

namespace ServiceRequests.Web.IntegrationTests;

/// <summary>
/// Uses its own container because the test stops it; the container is
/// disposed in DisposeAsync whether the test passes or fails.
/// </summary>
public sealed class DatabaseOutageTests : IAsyncLifetime
{
    private readonly PostgresContainer _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task When_the_database_becomes_unreachable_post_returns_a_generic_500_without_request_id()
    {
        await using var app = new RealDatabaseApp(_database.ConnectionString);
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await Requests.PostAsync(client, Requests.ValidJson)).StatusCode);

        await _database.StopAsync();
        var response = await Requests.PostAsync(client, Requests.ValidJson);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Talebinizin kaydedildiğini doğrulayamadık.", text);
        Assert.DoesNotContain("requestId", text);
        Assert.DoesNotContain("Host=", text);
        Assert.DoesNotContain("Password", text);
        Assert.DoesNotContain("Npgsql", text);
        Assert.DoesNotContain(" at ", text); // stack trace frames look like "   at Namespace.Method()"
    }
}
