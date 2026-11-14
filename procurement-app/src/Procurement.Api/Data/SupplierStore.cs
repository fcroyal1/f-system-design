using Dapper;
using Microsoft.Data.SqlClient;

namespace Procurement.Api.Data;

/// <summary>A supplier as stored: only the last 4 characters of the tax ID and bank account.</summary>
public sealed class SupplierRow
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
    public string LegalName { get; init; } = "";
    public string CategoryCode { get; init; } = "";
    public string CategoryName { get; init; } = "";
    public int PaymentTermsDays { get; init; }
    public string? TaxIdLast4 { get; init; }
    public string? BankLast4 { get; init; }
    public bool IsBlocked { get; init; }
}

public sealed record NewSupplier(
    Guid Id, string Code, string LegalName, string CategoryCode, int PaymentTermsDays, string? TaxIdLast4, string? BankLast4);

public sealed class SupplierStore(Db db)
{
    private const string SelectSupplier =
        """
        SELECT s.id AS Id, s.code AS Code, s.legal_name AS LegalName, s.category_code AS CategoryCode,
               c.name AS CategoryName, s.payment_terms_days AS PaymentTermsDays,
               s.tax_id_last4 AS TaxIdLast4, s.bank_last4 AS BankLast4, s.is_blocked AS IsBlocked
        FROM dbo.supplier s
        JOIN dbo.spend_category c ON c.code = s.category_code
        """;

    public async Task<IReadOnlyList<SupplierRow>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<SupplierRow>(new CommandDefinition(
            SelectSupplier + " ORDER BY s.code;", cancellationToken: cancellationToken))).ToList();
    }

    public async Task<SupplierRow?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<SupplierRow>(new CommandDefinition(
            SelectSupplier + " WHERE s.id = @id;", new { id }, cancellationToken: cancellationToken));
    }

    public static Task<SupplierRow?> FindAsync(SqlConnection connection, SqlTransaction transaction, Guid id) =>
        connection.QuerySingleOrDefaultAsync<SupplierRow>(SelectSupplier + " WHERE s.id = @id;", new { id }, transaction);

    public static Task<bool> CodeExistsAsync(SqlConnection connection, SqlTransaction transaction, string code) =>
        connection.ExecuteScalarAsync<bool>(
            "SELECT CAST(COUNT(*) AS bit) FROM dbo.supplier WITH (UPDLOCK, HOLDLOCK) WHERE code = @code;",
            new { code }, transaction);

    public static Task<bool> ExistsAsync(SqlConnection connection, SqlTransaction transaction, Guid id) =>
        connection.ExecuteScalarAsync<bool>(
            "SELECT CAST(COUNT(*) AS bit) FROM dbo.supplier WHERE id = @id;", new { id }, transaction);

    public static Task InsertAsync(SqlConnection connection, SqlTransaction transaction, NewSupplier supplier) =>
        connection.ExecuteAsync(
            """
            INSERT INTO dbo.supplier (id, code, legal_name, category_code, payment_terms_days, tax_id_last4, bank_last4, is_blocked)
            VALUES (@Id, @Code, @LegalName, @CategoryCode, @PaymentTermsDays, @TaxIdLast4, @BankLast4, 0);
            """,
            supplier, transaction);
}
