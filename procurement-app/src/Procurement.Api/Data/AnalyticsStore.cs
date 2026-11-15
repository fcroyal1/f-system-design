using Dapper;

namespace Procurement.Api.Data;

public sealed record CategorySpendRow(string Code, string Name, decimal Amount, int PoCount);

public sealed record SupplierSpendRow(Guid SupplierId, string Code, string Name, decimal Amount, int PoCount);

public sealed record StatusTotalRow(string Status, int Count, decimal Amount);

public sealed record BlockReasonRow(string Reason, int Count, decimal Amount);

public sealed record MonthRow(string Month, int PoCount, decimal PoAmount, int InvoiceCount, decimal InvoiceAmount);

public sealed record ApprovalRow(string Role, int PoCount, decimal Amount);

public sealed record DueRow(int DaysUntilDue, decimal Amount);

public sealed record SupplierCategoryRow(string Code, string Name, bool IsPoExempt, int Count, int Blocked);

public sealed record AnalyticsRows(
    IReadOnlyList<CategorySpendRow> SpendByCategory,
    IReadOnlyList<SupplierSpendRow> TopSuppliers,
    IReadOnlyList<StatusTotalRow> InvoicesByStatus,
    IReadOnlyList<BlockReasonRow> BlockReasons,
    IReadOnlyList<MonthRow> Monthly,
    IReadOnlyList<ApprovalRow> ApprovalsByRole,
    IReadOnlyList<DueRow> MatchedDue,
    IReadOnlyList<SupplierCategoryRow> SuppliersByCategory);

/// <summary>
/// Read-only aggregates for the analytics screen. Spend is committed spend: the line totals of every PO
/// that is no longer a Draft. An invoice's amount is its line total plus tax.
/// </summary>
public sealed class AnalyticsStore(Db db)
{
    private const int TopSupplierCount = 8;

    private const string Sql =
        """
        WITH po_total AS (
            SELECT po.id, po.supplier_id, po.po_date, po.approver_role,
                   SUM(l.quantity * l.unit_price) AS amount
            FROM dbo.purchase_order po
            JOIN dbo.po_line l ON l.po_id = po.id
            WHERE po.status <> 'Draft'
            GROUP BY po.id, po.supplier_id, po.po_date, po.approver_role
        )
        SELECT * INTO #po FROM po_total;

        SELECT i.id, i.status, i.block_reason, i.invoice_date, i.due_date,
               ISNULL((SELECT SUM(il.quantity * il.unit_price) FROM dbo.invoice_line il WHERE il.invoice_id = i.id), 0)
                   + i.tax_amount AS amount
        INTO #inv
        FROM dbo.invoice i;

        -- 1. Spend by the supplier's category
        SELECT c.code AS Code, c.name AS Name, SUM(p.amount) AS Amount, COUNT(*) AS PoCount
        FROM #po p
        JOIN dbo.supplier s ON s.id = p.supplier_id
        JOIN dbo.spend_category c ON c.code = s.category_code
        GROUP BY c.code, c.name
        ORDER BY Amount DESC, c.code;

        -- 2. Top suppliers by spend
        SELECT TOP (@top) s.id AS SupplierId, s.code AS Code, s.legal_name AS Name, SUM(p.amount) AS Amount, COUNT(*) AS PoCount
        FROM #po p
        JOIN dbo.supplier s ON s.id = p.supplier_id
        GROUP BY s.id, s.code, s.legal_name
        ORDER BY Amount DESC, s.code;

        -- 3. Invoices by status
        SELECT status AS Status, COUNT(*) AS Count, SUM(amount) AS Amount
        FROM #inv
        GROUP BY status
        ORDER BY status;

        -- 4. Why invoices are blocked: the check name before the first colon of the block reason
        SELECT reason AS Reason, COUNT(*) AS Count, SUM(amount) AS Amount
        FROM (
            SELECT amount,
                   CASE WHEN CHARINDEX(':', block_reason) > 1
                        THEN LEFT(block_reason, CHARINDEX(':', block_reason) - 1)
                        ELSE ISNULL(block_reason, N'Unknown') END AS reason
            FROM #inv
            WHERE status = 'Blocked'
        ) b
        GROUP BY reason
        ORDER BY Count DESC, Amount DESC, reason;

        -- 5. Monthly POs (by PO date) and invoices (by invoice date)
        SELECT Month, SUM(PoCount) AS PoCount, SUM(PoAmount) AS PoAmount,
               SUM(InvoiceCount) AS InvoiceCount, SUM(InvoiceAmount) AS InvoiceAmount
        FROM (
            SELECT CONVERT(char(7), po_date, 126) AS Month, 1 AS PoCount, amount AS PoAmount, 0 AS InvoiceCount, 0 AS InvoiceAmount
            FROM #po
            UNION ALL
            SELECT CONVERT(char(7), invoice_date, 126), 0, 0, 1, amount
            FROM #inv
        ) m
        GROUP BY Month
        ORDER BY Month;

        -- 6. Who approved how much
        SELECT ISNULL(approver_role, N'Not routed') AS Role, COUNT(*) AS PoCount, SUM(amount) AS Amount
        FROM #po
        GROUP BY approver_role
        ORDER BY Amount DESC;

        -- 7. Matched invoices by days until due (bucketed in C#)
        SELECT DATEDIFF(day, CAST(SYSUTCDATETIME() AS date), due_date) AS DaysUntilDue, amount AS Amount
        FROM #inv
        WHERE status = 'Matched';

        -- 8. Supplier base by category, including empty categories
        SELECT c.code AS Code, c.name AS Name, c.is_po_exempt AS IsPoExempt,
               COUNT(s.id) AS Count, ISNULL(SUM(CAST(s.is_blocked AS int)), 0) AS Blocked
        FROM dbo.spend_category c
        LEFT JOIN dbo.supplier s ON s.category_code = c.code
        GROUP BY c.code, c.name, c.is_po_exempt
        ORDER BY Count DESC, c.code;
        """;

    public async Task<AnalyticsRows> ReadAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenAsync(cancellationToken);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Sql, new { top = TopSupplierCount }, cancellationToken: cancellationToken));

        return new AnalyticsRows(
            (await results.ReadAsync<CategorySpendRow>()).ToList(),
            (await results.ReadAsync<SupplierSpendRow>()).ToList(),
            (await results.ReadAsync<StatusTotalRow>()).ToList(),
            (await results.ReadAsync<BlockReasonRow>()).ToList(),
            (await results.ReadAsync<MonthRow>()).ToList(),
            (await results.ReadAsync<ApprovalRow>()).ToList(),
            (await results.ReadAsync<DueRow>()).ToList(),
            (await results.ReadAsync<SupplierCategoryRow>()).ToList());
    }
}
