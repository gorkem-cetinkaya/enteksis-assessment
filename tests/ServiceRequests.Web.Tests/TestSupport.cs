using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ServiceRequests.Web.Data;
using ServiceRequests.Web.Requests;

namespace ServiceRequests.Web.Tests;

/// <summary>Shared request helpers. All data is fictional; example.com is a reserved domain.</summary>
internal static class TestRequests
{
    public const string ValidJson = """
        {"name":"  Deniz Örnek  ","email":"deniz.ornek@example.com","service":"ai-triage","description":"Gelen talepleri otomatik sınıflandırmak istiyoruz."}
        """;

    public static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string json) =>
        client.PostAsync("/api/requests", new StringContent(json, Encoding.UTF8, "application/json"));
}

/// <summary>
/// The app with the given store instead of PostgreSQL and without the startup
/// schema step. Extra configuration values can be passed as settings.
/// </summary>
internal sealed class TestApp(IServiceRequestStore store, IReadOnlyDictionary<string, string?>? settings = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IServiceRequestStore>();
            services.AddSingleton(store);
            services.Remove(services.Single(service => service.ImplementationType == typeof(DatabaseInitializer)));
        });
    }
}

internal sealed class RecordingStore : IServiceRequestStore
{
    public Guid ReturnedId { get; } = Guid.NewGuid();

    public List<ServiceRequestInput> Saved { get; } = [];

    public Task<Guid> InsertAsync(ServiceRequestInput request, CancellationToken cancellationToken)
    {
        lock (Saved)
        {
            Saved.Add(request);
        }

        return Task.FromResult(ReturnedId);
    }
}

internal sealed class FailingStore : IServiceRequestStore
{
    public const string SecretDetail = "Password=super-secret";

    public Task<Guid> InsertAsync(ServiceRequestInput request, CancellationToken cancellationToken) =>
        throw new NpgsqlException($"INSERT INTO service_requests failed (Host=db;{SecretDetail})");
}

/// <summary>A request body whose length is not known up front, so it is sent without Content-Length.</summary>
internal sealed class UnknownLengthContent : HttpContent
{
    private readonly byte[] _bytes;

    public UnknownLengthContent(byte[] bytes, string mediaType)
    {
        _bytes = bytes;
        Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        stream.WriteAsync(_bytes).AsTask();

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
