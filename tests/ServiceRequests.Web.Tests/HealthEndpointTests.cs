using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceRequests.Web.Requests;
using static ServiceRequests.Web.Tests.TestRequests;

namespace ServiceRequests.Web.Tests;

public class HealthEndpointTests
{
    [Fact]
    public async Task Health_shows_only_status_and_a_null_commit_when_render_does_not_set_one()
    {
        await using var app = new TestApp(new RecordingStore(), new Dictionary<string, string?> { ["RENDER_GIT_COMMIT"] = "" });
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["commit", "status"], body.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("commit").ValueKind);
    }

    [Fact]
    public async Task Health_shows_the_commit_from_render_git_commit()
    {
        const string commit = "0123456789abcdef0123456789abcdef01234567"; // test value, not a real commit
        await using var app = new TestApp(new RecordingStore(), new Dictionary<string, string?> { ["RENDER_GIT_COMMIT"] = commit });
        using var client = app.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>("/health");

        Assert.Equal(commit, body.GetProperty("commit").GetString());
    }

    [Fact]
    public async Task Health_is_not_rate_limited()
    {
        await using var app = new TestApp(new RecordingStore());
        using var client = app.CreateClient();
        for (var i = 0; i <= RequestLimits.PermitLimit; i++)
        {
            await PostJsonAsync(client, ValidJson);
        }

        for (var i = 0; i <= RequestLimits.PermitLimit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        }
    }
}
