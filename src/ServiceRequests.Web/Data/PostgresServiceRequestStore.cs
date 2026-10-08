using Npgsql;
using ServiceRequests.Web.Requests;

namespace ServiceRequests.Web.Data;

public sealed class PostgresServiceRequestStore(NpgsqlDataSource dataSource) : IServiceRequestStore
{
    // Values are sent as parameters, never concatenated into the SQL text.
    // id and created_at are filled in by the database defaults.
    private const string InsertSql = """
        INSERT INTO service_requests (name, email, service, description)
        VALUES (@name, @email, @service, @description)
        RETURNING id
        """;

    public async Task<Guid> InsertAsync(ServiceRequestInput request, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(InsertSql, connection, transaction);
        command.Parameters.AddWithValue("name", request.Name);
        command.Parameters.AddWithValue("email", request.Email);
        command.Parameters.AddWithValue("service", request.Service);
        command.Parameters.AddWithValue("description", request.Description);

        var id = (Guid)(await command.ExecuteScalarAsync(cancellationToken))!;

        // The id leaves this method only after COMMIT succeeds.
        await transaction.CommitAsync(cancellationToken);
        return id;
    }
}
