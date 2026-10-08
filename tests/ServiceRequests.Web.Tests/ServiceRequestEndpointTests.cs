using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ServiceRequests.Web.Data;
using ServiceRequests.Web.Requests;

namespace ServiceRequests.Web.Tests;

/// <summary>
/// Runs the real HTTP pipeline in memory with the PostgreSQL store replaced by
/// a fake, to check what the endpoint returns for each outcome. Persistence in
/// a real database is checked separately (see README).
/// </summary>
public class ServiceRequestEndpointTests
{
    private const string ValidJson = """
        {"name":"  Deniz Örnek  ","email":"deniz.ornek@example.com","service":"ai-triage","description":"Gelen talepleri otomatik sınıflandırmak istiyoruz."}
        """;

    [Fact]
    public async Task Valid_request_returns_201_with_the_id_returned_by_the_store()
    {
        var store = new RecordingStore();
        await using var app = new TestApp(store);
        using var client = app.CreateClient();

        var response = await PostJsonAsync(client, ValidJson);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(store.ReturnedId, body.GetProperty("requestId").GetGuid());

        var saved = Assert.Single(store.Saved);
        Assert.Equal(
            new ServiceRequestInput(
                "Deniz Örnek", "deniz.ornek@example.com", "ai-triage", "Gelen talepleri otomatik sınıflandırmak istiyoruz."),
            saved);
    }

    [Fact]
    public async Task Invalid_request_returns_400_with_field_errors_and_saves_nothing()
    {
        var store = new RecordingStore();
        await using var app = new TestApp(store);
        using var client = app.CreateClient();

        var response = await PostJsonAsync(client, """{"name":"D","email":42,"service":"consulting"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("Ad soyad en az 2 karakter olmalıdır.", errors.GetProperty("name")[0].GetString());
        Assert.Equal("E-posta metin olmalıdır.", errors.GetProperty("email")[0].GetString());
        Assert.Equal("Geçerli bir hizmet seçin.", errors.GetProperty("service")[0].GetString());
        Assert.Equal("Açıklama zorunludur.", errors.GetProperty("description")[0].GetString());

        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Email_with_invalid_characters_in_the_domain_returns_400_and_saves_nothing()
    {
        var store = new RecordingStore();
        await using var app = new TestApp(store);
        using var client = app.CreateClient();

        var response = await PostJsonAsync(client, ValidJson.Replace("deniz.ornek@example.com", "codex-review@exa/mple.com"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("Geçerli bir e-posta adresi girin.", errors.GetProperty("email")[0].GetString());
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Email_with_angle_brackets_in_the_local_part_returns_400_and_saves_nothing()
    {
        var store = new RecordingStore();
        await using var app = new TestApp(store);
        using var client = app.CreateClient();

        var response = await PostJsonAsync(client, ValidJson.Replace("deniz.ornek@example.com", "codex<phase2>@example.com"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("Geçerli bir e-posta adresi girin.", errors.GetProperty("email")[0].GetString());
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Email_with_a_letter_outside_the_bmp_in_the_domain_is_saved()
    {
        // The browser accepts codex@𐐀.example (U+10400); the server must agree.
        var store = new RecordingStore();
        await using var app = new TestApp(store);
        using var client = app.CreateClient();

        var response = await PostJsonAsync(client, ValidJson.Replace("deniz.ornek@example.com", "codex@\U00010400.example"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("codex@\U00010400.example", Assert.Single(store.Saved).Email);
    }

    [Fact]
    public async Task Email_with_subdomain_and_plus_is_saved()
    {
        var store = new RecordingStore();
        await using var app = new TestApp(store);
        using var client = app.CreateClient();

        var response = await PostJsonAsync(client, ValidJson.Replace("deniz.ornek@example.com", "deniz+test@mail.example.com.tr"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("deniz+test@mail.example.com.tr", Assert.Single(store.Saved).Email);
    }

    [Theory]
    [InlineData("""{"name":""")]
    [InlineData("[]")]
    [InlineData("\"metin\"")]
    [InlineData("")]
    public async Task Body_that_is_not_a_json_object_returns_400_and_saves_nothing(string body)
    {
        var store = new RecordingStore();
        await using var app = new TestApp(store);
        using var client = app.CreateClient();

        var response = await PostJsonAsync(client, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Non_json_content_type_returns_415_and_saves_nothing()
    {
        var store = new RecordingStore();
        await using var app = new TestApp(store);
        using var client = app.CreateClient();

        var response = await client.PostAsync("/api/requests", new StringContent(ValidJson, Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Database_failure_returns_500_without_internal_details()
    {
        await using var app = new TestApp(new FailingStore());
        using var client = app.CreateClient();

        var response = await PostJsonAsync(client, ValidJson);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Talebinizin kaydedildiğini doğrulayamadık.", text);
        Assert.DoesNotContain("requestId", text);
        Assert.DoesNotContain(FailingStore.SecretDetail, text);
        Assert.DoesNotContain("INSERT", text);
        Assert.DoesNotContain(nameof(NpgsqlException), text);
        Assert.DoesNotContain(" at ", text); // stack trace frames look like "   at Namespace.Method()"
    }

    [Fact]
    public async Task Stored_requests_cannot_be_listed()
    {
        await using var app = new TestApp(new RecordingStore());
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/requests");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Home_page_serves_the_landing_sections_and_the_form_with_the_demo_notice()
    {
        var html = await GetHomePageAsync();

        Assert.Single(Regex.Matches(html, "<h1[ >]"));
        Assert.Contains("""<a class="button" href="#talep">Talep oluştur</a>""", html);
        foreach (var section in new[] { "hizmetler", "ornek", "surec", "talep" })
        {
            Assert.Contains($"""<section id="{section}" """, html);
        }

        Assert.Contains("""<form id="request-form" method="post" action="/api/requests" """, html);
        Assert.Contains("Demo uygulamadır. Yalnızca kurgusal bilgiler kullanın.", html);
    }

    [Theory]
    [InlineData("workflow-automation", "İş akışı otomasyonu")]
    [InlineData("api-integration", "API ve veri entegrasyonu")]
    [InlineData("ai-triage", "AI destekli talep sınıflandırma")]
    public async Task Each_service_card_matches_a_form_option(string code, string name)
    {
        var html = await GetHomePageAsync();

        Assert.Contains($"""<h3 class="card-title">{name}</h3>""", html);
        Assert.Contains($"""<option value="{code}">{name}</option>""", html);
    }

    [Fact]
    public async Task Form_options_are_exactly_the_service_codes_the_server_accepts()
    {
        var html = await GetHomePageAsync();

        var optionValues = Regex.Matches(html, """<option value="([^"]+)">""").Select(match => match.Groups[1].Value);
        Assert.Equal(ServiceRequestValidator.ServiceCodes, optionValues);
    }

    private static async Task<string> GetHomePageAsync()
    {
        await using var app = new TestApp(new RecordingStore());
        using var client = app.CreateClient();
        return await client.GetStringAsync("/");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/app.js")]
    [InlineData("/validation.js")]
    [InlineData("/styles.css")]
    public async Task Static_files_must_be_revalidated_by_the_browser(string path)
    {
        await using var app = new TestApp(new RecordingStore());
        using var client = app.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoCache, $"Expected Cache-Control: no-cache for {path}.");
    }

    private static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string json) =>
        client.PostAsync("/api/requests", new StringContent(json, Encoding.UTF8, "application/json"));

    /// <summary>The app with the given store and without the startup schema step.</summary>
    private sealed class TestApp(IServiceRequestStore store) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IServiceRequestStore>();
                services.AddSingleton(store);
                services.Remove(services.Single(service => service.ImplementationType == typeof(DatabaseInitializer)));
            });
    }

    private sealed class RecordingStore : IServiceRequestStore
    {
        public Guid ReturnedId { get; } = Guid.NewGuid();

        public List<ServiceRequestInput> Saved { get; } = [];

        public Task<Guid> InsertAsync(ServiceRequestInput request, CancellationToken cancellationToken)
        {
            Saved.Add(request);
            return Task.FromResult(ReturnedId);
        }
    }

    private sealed class FailingStore : IServiceRequestStore
    {
        public const string SecretDetail = "Password=super-secret";

        public Task<Guid> InsertAsync(ServiceRequestInput request, CancellationToken cancellationToken) =>
            throw new NpgsqlException($"INSERT INTO service_requests failed (Host=db;{SecretDetail})");
    }
}
