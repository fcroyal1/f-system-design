using Dapper;
using Microsoft.Data.SqlClient;
using Procurement.Domain.Policies;

namespace Procurement.Api.Data;

public sealed record SpendCategoryDto(string Code, string Name, bool IsPoExempt);

public sealed record GlAccountDto(string Number, string Name, string Type);

public sealed record CostCenterDto(string Code, string Name, string OwnerName);

public sealed record DoaTierDto(decimal MaxAmount, string ApproverRole);

public sealed record ReferenceDataDto(
    IReadOnlyList<SpendCategoryDto> Categories,
    IReadOnlyList<GlAccountDto> GlAccounts,
    IReadOnlyList<CostCenterDto> CostCenters,
    IReadOnlyList<DoaTierDto> DoaTiers);

/// <summary>Spend categories, GL accounts, cost centers and DoA tiers.</summary>
public sealed class ReferenceStore(Db db)
{
    public async Task<ReferenceDataDto> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenAsync(cancellationToken);
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT code AS Code, name AS Name, is_po_exempt AS IsPoExempt FROM dbo.spend_category ORDER BY code;
            SELECT number AS Number, name AS Name, type AS Type FROM dbo.gl_account ORDER BY number;
            SELECT code AS Code, name AS Name, owner_name AS OwnerName FROM dbo.cost_center ORDER BY code;
            SELECT max_amount AS MaxAmount, approver_role AS ApproverRole FROM dbo.doa_tier ORDER BY max_amount;
            """,
            cancellationToken: cancellationToken));

        var categories = (await results.ReadAsync<CategoryRow>()).Select(r => new SpendCategoryDto(r.Code, r.Name, r.IsPoExempt)).ToList();
        var glAccounts = (await results.ReadAsync<GlAccountRow>()).Select(r => new GlAccountDto(r.Number, r.Name, r.Type)).ToList();
        var costCenters = (await results.ReadAsync<CostCenterRow>()).Select(r => new CostCenterDto(r.Code, r.Name, r.OwnerName)).ToList();
        var tiers = (await results.ReadAsync<TierRow>()).Select(r => new DoaTierDto(r.MaxAmount, r.ApproverRole)).ToList();
        return new ReferenceDataDto(categories, glAccounts, costCenters, tiers);
    }

    /// <summary>The DoA tiers as the domain router expects them, read inside the caller's transaction.</summary>
    public static async Task<IReadOnlyList<DoaTier>> GetDoaTiersAsync(SqlConnection connection, SqlTransaction transaction) =>
        (await connection.QueryAsync<TierRow>(
            "SELECT max_amount AS MaxAmount, approver_role AS ApproverRole FROM dbo.doa_tier ORDER BY max_amount;",
            transaction: transaction))
        .Select(r => new DoaTier(r.MaxAmount, r.ApproverRole))
        .ToList();

    public static Task<bool> CategoryExistsAsync(SqlConnection connection, SqlTransaction transaction, string code) =>
        connection.ExecuteScalarAsync<bool>(
            "SELECT CAST(COUNT(*) AS bit) FROM dbo.spend_category WHERE code = @code;", new { code }, transaction);

    public static Task<bool> CostCenterExistsAsync(SqlConnection connection, SqlTransaction transaction, string code) =>
        connection.ExecuteScalarAsync<bool>(
            "SELECT CAST(COUNT(*) AS bit) FROM dbo.cost_center WHERE code = @code;", new { code }, transaction);

    public static async Task<IReadOnlySet<string>> GlAccountNumbersAsync(SqlConnection connection, SqlTransaction transaction) =>
        (await connection.QueryAsync<string>("SELECT number FROM dbo.gl_account;", transaction: transaction))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private sealed class CategoryRow
    {
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public bool IsPoExempt { get; init; }
    }

    private sealed class GlAccountRow
    {
        public string Number { get; init; } = "";
        public string Name { get; init; } = "";
        public string Type { get; init; } = "";
    }

    private sealed class CostCenterRow
    {
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string OwnerName { get; init; } = "";
    }

    private sealed class TierRow
    {
        public decimal MaxAmount { get; init; }
        public string ApproverRole { get; init; } = "";
    }
}
