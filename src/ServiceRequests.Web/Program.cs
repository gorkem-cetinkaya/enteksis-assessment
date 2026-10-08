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

app.Run();

// Allows the test project to start the app with WebApplicationFactory<Program>.
public partial class Program;
