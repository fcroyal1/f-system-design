using Microsoft.Data.SqlClient;

namespace Procurement.Api.Data;

/// <summary>Opens connections to the Procurement database (config <c>ConnectionStrings:Procurement</c>).</summary>
public sealed class Db(IConfiguration configuration)
{
    private string ConnectionString =>
        configuration.GetConnectionString("Procurement")
        ?? throw new InvalidOperationException("Connection string 'Procurement' is not configured.");

    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>Runs <paramref name="work"/> in one transaction; commits when it returns, rolls back when it throws.</summary>
    public async Task<T> InTransactionAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> work, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var result = await work(connection, transaction);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}

/// <summary>The user recorded on approvals and audit rows (config <c>Actor:Name</c>).</summary>
public sealed record Actor(string Name)
{
    public static Actor FromConfiguration(IConfiguration configuration) =>
        new(string.IsNullOrWhiteSpace(configuration["Actor:Name"]) ? "Demo User" : configuration["Actor:Name"]!);
}
