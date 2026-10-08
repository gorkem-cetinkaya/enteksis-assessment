using Npgsql;
using ServiceRequests.Web.Data;
using ServiceRequests.Web.Requests;

var builder = WebApplication.CreateBuilder(args);

// Unhandled exceptions are turned into a generic ProblemDetails response.
builder.Services.AddProblemDetails();

// Read from configuration, e.g. the ConnectionStrings__Postgres environment variable.
builder.Services.AddSingleton(serviceProvider =>
{
    var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Postgres");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Connection string 'Postgres' is not configured. Set the ConnectionStrings__Postgres environment variable.");
    }

    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    // Kerberos/GSS is not used. Without this, Npgsql probes for libgssapi_krb5,
    // which the runtime image does not ship, and logs a native load error.
    dataSourceBuilder.ConnectionStringBuilder.GssEncryptionMode = GssEncryptionMode.Disable;
    return dataSourceBuilder.Build();
});
builder.Services.AddSingleton<IServiceRequestStore, PostgresServiceRequestStore>();
builder.Services.AddHostedService<DatabaseInitializer>();
builder.Services.AddServiceRequestRateLimiting();

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // Without a Cache-Control header browsers may reuse a cached app.js after
    // a new version is deployed. "no-cache" makes them revalidate on every
    // load; an unchanged file is answered with a cheap 304 via its ETag.
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
});
// Only endpoints that opt in (POST /api/requests) are rate-limited.
app.UseRateLimiter();
app.MapServiceRequestEndpoints();

try
{
    app.Run();
}
catch (Exception)
{
    // The host has already logged the cause (e.g. database unreachable at
    // startup). Return an exit code instead of letting the exception escape.
    // In our Docker test, an unhandled startup exception left the process
    // running at ~100% CPU while dotnet was PID 1 (under docker run --init it
    // exited); the root cause was not investigated further.
    app.Logger.LogCritical("Startup failed; exiting with code 1.");
    return 1;
}

return 0;

// Allows the test project to start the app with WebApplicationFactory<Program>.
public partial class Program;
