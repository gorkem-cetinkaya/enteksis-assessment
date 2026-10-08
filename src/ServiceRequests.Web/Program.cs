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

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapServiceRequestEndpoints();

try
{
    app.Run();
}
catch (Exception)
{
    // The host has already logged the cause (e.g. database unreachable at
    // startup). Exit with a code instead of letting the exception escape:
    // in a container where dotnet is PID 1, an unhandled exception leaves the
    // process hung at 100% CPU instead of stopping it.
    app.Logger.LogCritical("Startup failed; exiting with code 1.");
    return 1;
}

return 0;

// Allows the test project to start the app with WebApplicationFactory<Program>.
public partial class Program;
