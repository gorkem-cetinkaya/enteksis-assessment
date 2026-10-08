using Npgsql;

namespace ServiceRequests.Web.Data;

/// <summary>
/// Applies Data/schema.sql before the app starts serving requests. The script
/// is idempotent, so running it on every start keeps existing data intact.
/// If the database is unreachable the app fails to start instead of running
/// without a table.
/// </summary>
public sealed class DatabaseInitializer(NpgsqlDataSource dataSource, ILogger<DatabaseInitializer> logger) : IHostedService
{
    private static readonly string SchemaPath = Path.Combine(AppContext.BaseDirectory, "Data", "schema.sql");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var schemaSql = await File.ReadAllTextAsync(SchemaPath, cancellationToken);

        await using var command = dataSource.CreateCommand(schemaSql);
        await command.ExecuteNonQueryAsync(cancellationToken);

        logger.LogInformation("Database schema applied from {SchemaPath}", SchemaPath);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
