using System.Net;
using System.Text;
using ServiceRequests.Web.Requests;
using static ServiceRequests.Web.Tests.TestRequests;

namespace ServiceRequests.Web.Tests;

/// <summary>CSP, nosniff and Referrer-Policy on normal and error responses.</summary>
public class SecurityHeaderTests
{
    [Theory]
    [InlineData("/", HttpStatusCode.OK)]
    [InlineData("/app.js", HttpStatusCode.OK)]
    [InlineData("/styles.css", HttpStatusCode.OK)]
    [InlineData("/does-not-exist", HttpStatusCode.NotFound)]
    public async Task Get_responses_carry_the_security_headers(string path, HttpStatusCode expectedStatus)
    {
        await using var app = new TestApp(new RecordingStore());
        using var client = app.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(expectedStatus, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task Created_and_validation_error_responses_carry_the_security_headers()
    {
        await using var app = new TestApp(new RecordingStore());
        using var client = app.CreateClient();

        var created = await PostJsonAsync(client, ValidJson);
        var invalid = await PostJsonAsync(client, """{"name":"D"}""");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        AssertSecurityHeaders(created);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        AssertSecurityHeaders(invalid);
    }

    [Fact]
    public async Task Server_error_responses_carry_the_security_headers()
    {
        await using var app = new TestApp(new FailingStore());
        using var client = app.CreateClient();

        var response = await PostJsonAsync(client, ValidJson);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task Too_large_and_rate_limited_responses_carry_the_security_headers()
    {
        await using var app = new TestApp(new RecordingStore());
        using var client = app.CreateClient();

        var tooLarge = await PostJsonAsync(client, ValidJson + new string(' ', RequestLimits.MaxBodyBytes));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
        AssertSecurityHeaders(tooLarge);

        HttpResponseMessage response;
        do
        {
            response = await PostJsonAsync(client, ValidJson);
        }
        while (response.StatusCode != HttpStatusCode.TooManyRequests);

        AssertSecurityHeaders(response);
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }
}
