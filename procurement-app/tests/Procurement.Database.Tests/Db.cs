using Microsoft.Data.SqlClient;

namespace Procurement.Database.Tests;

internal static class Db
{
    public static async Task<SqlConnection> OpenAsync(string connectionString)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    public static async Task<T> ScalarAsync<T>(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return (T)value!;
    }

    public static async Task<int> ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        return await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<List<object[]>> RowsAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var rows = new List<object[]>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row);
        }

        return rows;
    }
}
