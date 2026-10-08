using System.Net;
using System.Text;
using ServiceRequests.Web.Requests;
using static ServiceRequests.Web.Tests.TestRequests;

namespace ServiceRequests.Web.Tests;

/// <summary>Rate limit and body size limit of POST /api/requests (in-memory server, fake store).</summary>
public class RequestLimitTests
{
    [Fact]
    public async Task The_21st_post_within_the_window_gets_429_with_retry_after_and_is_not_saved()
    {
        var store = new RecordingStore();
        await using var app = new TestApp(store);
        using var client = app.CreateClient();

        for (var i = 0; i < RequestLimits.PermitLimit; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await PostJsonAsync(client, ValidJson)).StatusCode);
        }

        var limited = await PostJsonAsync(client, ValidJson);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        Assert.InRange(limited.Headers.RetryAfter?.Delta ?? TimeSpan.Zero, TimeSpan.FromSeconds(1), RequestLimits.Window);
        Assert.Contains("Kısa sürede çok fazla talep gönderildi.", await limited.Content.ReadAsStringAsync());
        Assert.Equal(RequestLimits.PermitLimit, store.Saved.Count);
    }

    [Fact]
    public async Task The_page_and_static_files_are_not_rate_limited()
    {
        await using var app = new TestApp(new RecordingStore());
        using var client = app.CreateClient();
        for (var i = 0; i <= RequestLimits.PermitLimit; i++)
        {
            await PostJsonAsync(client, ValidJson);
        }

        foreach (var path in new[] { "/", "/app.js", "/styles.css" })
        {
            for (var i = 0; i <= RequestLimits.PermitLimit; i++)
            {
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
            }
        }
    }

    [Fact]
    public async Task Body_of_exactly_32_KiB_is_accepted()
    {
        var store = new RecordingStore();
        await using var app = new TestApp(store);
        using var client = app.CreateClient();

        var response = await PostJsonAsync(client, PadToBytes(ValidJson, RequestLimits.MaxBodyBytes));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(store.Saved);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)] // no Content-Length: the limit is found while reading
    public async Task Body_over_32_KiB_gets_413_and_is_not_saved(bool sendContentLength)
    {
        var store = new RecordingStore();
        await using var app = new TestApp(store);
        using var client = app.CreateClient();
        var json = PadToBytes(ValidJson, RequestLimits.MaxBodyBytes + 1);
        HttpContent content = sendContentLength
            ? new StringContent(json, Encoding.UTF8, "application/json")
            : new UnknownLengthContent(Encoding.UTF8.GetBytes(json), "application/json");

        var response = await client.PostAsync("/api/requests", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("En fazla 32 KiB", await response.Content.ReadAsStringAsync());
        Assert.Empty(store.Saved);
    }

    // Trailing spaces keep the JSON valid while reaching an exact UTF-8 size.
    private static string PadToBytes(string json, int bytes) =>
        json + new string(' ', bytes - Encoding.UTF8.GetByteCount(json));
}
