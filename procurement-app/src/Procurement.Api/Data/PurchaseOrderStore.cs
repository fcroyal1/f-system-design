using Dapper;
using Microsoft.Data.SqlClient;

namespace Procurement.Api.Data;

public sealed class PurchaseOrderRow
{
    public Guid Id { get; init; }
    public string Number { get; init; } = "";
    public Guid SupplierId { get; init; }
    public string SupplierCode { get; init; } = "";
    public string SupplierName { get; init; } = "";
    public string RequesterName { get; init; } = "";
    public string CostCenterCode { get; init; } = "";
    public string Currency { get; init; } = "";
    public DateTime PoDate { get; init; }
    public string Status { get; init; } = "";
    public string? ApproverRole { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTime? ApprovedAt { get; init; }

    /// <summary>Σ quantity × unit_price, unrounded.</summary>
    public decimal Total { get; init; }
}

public sealed class PoLineRow
{
    public int LineNo { get; init; }
    public string ItemCode { get; init; } = "";
    public string Description { get; init; } = "";
    public string Uom { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string GlAccount { get; init; } = "";
    public bool ReceiptRequired { get; init; }
}

public sealed class ReceiptSummaryRow
{
    public Guid Id { get; init; }
    public string GrNumber { get; init; } = "";
    public DateTime ReceivedAt { get; init; }
    public string ReceivedBy { get; init; } = "";
    public string? PackingSlip { get; init; }
    public decimal QuantityReceived { get; init; }
}

public sealed class InvoiceSummaryRow
{
    public Guid Id { get; init; }
    public string InvoiceNumber { get; init; } = "";
    public DateTime InvoiceDate { get; init; }
    public DateTime DueDate { get; init; }
    public string Currency { get; init; } = "";
    public string Status { get; init; } = "";
    public string? BlockReason { get; init; }
    public bool IsRetroPo { get; init; }

    /// <summary>Σ line quantity × unit_price + tax, unrounded.</summary>
    public decimal Total { get; init; }
}

public sealed class ApprovalCandidateRow
{
    public string Number { get; init; } = "";
    public string Status { get; init; } = "";
    public decimal Total { get; init; }
}

public sealed record PurchaseOrderDetailRows(
    PurchaseOrderRow Header,
    IReadOnlyList<PoLineRow> Lines,
    IReadOnlyList<ReceiptSummaryRow> Receipts,
    IReadOnlyList<InvoiceSummaryRow> Invoices);

public sealed record NewPoLine(
    int LineNo, string ItemCode, string Description, string Uom, decimal Quantity, decimal UnitPrice, string GlAccount, bool ReceiptRequired);

public sealed record NewPurchaseOrder(
    Guid Id, Guid SupplierId, string RequesterName, string CostCenterCode, string Currency, DateTime PoDate, IReadOnlyList<NewPoLine> Lines);

public sealed class PurchaseOrderStore(Db db)
{
    private const string SelectHeader =
        """
        SELECT po.id AS Id, po.po_number AS Number, po.supplier_id AS SupplierId, s.code AS SupplierCode,
               s.legal_name AS SupplierName, po.requester_name AS RequesterName, po.cost_center_code AS CostCenterCode,
               po.currency AS Currency, po.po_date AS PoDate, po.status AS Status, po.approver_role AS ApproverRole,
               po.approved_by AS ApprovedBy, po.approved_at AS ApprovedAt,
               ISNULL((SELECT SUM(l.quantity * l.unit_price) FROM dbo.po_line l WHERE l.po_id = po.id), 0) AS Total
        FROM dbo.purchase_order po
        JOIN dbo.supplier s ON s.id = po.supplier_id
        """;

    public async Task<IReadOnlyList<PurchaseOrderRow>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<PurchaseOrderRow>(new CommandDefinition(
            SelectHeader + " ORDER BY po.po_number DESC;", cancellationToken: cancellationToken))).ToList();
    }

    public async Task<PurchaseOrderDetailRows?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenAsync(cancellationToken);
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            SelectHeader + " WHERE po.id = @id;" +
            """

            SELECT line_no AS [LineNo], item_code AS ItemCode, description AS Description, uom AS Uom,
                   quantity AS Quantity, unit_price AS UnitPrice, gl_account AS GlAccount, receipt_required AS ReceiptRequired
            FROM dbo.po_line WHERE po_id = @id ORDER BY line_no;

            SELECT gr.id AS Id, gr.gr_number AS GrNumber, gr.received_at AS ReceivedAt, gr.received_by AS ReceivedBy,
                   gr.packing_slip AS PackingSlip,
                   ISNULL((SELECT SUM(gl.quantity_received) FROM dbo.gr_line gl WHERE gl.gr_id = gr.id), 0) AS QuantityReceived
            FROM dbo.goods_receipt gr WHERE gr.po_id = @id ORDER BY gr.received_at, gr.gr_number;

            SELECT i.id AS Id, i.invoice_number AS InvoiceNumber, i.invoice_date AS InvoiceDate, i.due_date AS DueDate,
                   i.currency AS Currency, i.status AS Status, i.block_reason AS BlockReason, i.is_retro_po AS IsRetroPo,
                   ISNULL((SELECT SUM(il.quantity * il.unit_price) FROM dbo.invoice_line il WHERE il.invoice_id = i.id), 0)
                       + i.tax_amount AS Total
            FROM dbo.invoice i WHERE i.po_id = @id ORDER BY i.invoice_date, i.invoice_number;
            """,
            new { id }, cancellationToken: cancellationToken));

        var header = await results.ReadSingleOrDefaultAsync<PurchaseOrderRow>();
        if (header is null)
        {
            return null;
        }

        var lines = (await results.ReadAsync<PoLineRow>()).ToList();
        var receipts = (await results.ReadAsync<ReceiptSummaryRow>()).ToList();
        var invoices = (await results.ReadAsync<InvoiceSummaryRow>()).ToList();
        return new PurchaseOrderDetailRows(header, lines, receipts, invoices);
    }

    /// <summary>Inserts a Draft PO numbered from <c>po_number_seq</c>; returns the number.</summary>
    public static async Task<string> InsertDraftAsync(SqlConnection connection, SqlTransaction transaction, NewPurchaseOrder po)
    {
        var number = await connection.ExecuteScalarAsync<string>(
            """
            DECLARE @number varchar(10) = CAST(NEXT VALUE FOR dbo.po_number_seq AS varchar(10));
            INSERT INTO dbo.purchase_order (id, po_number, supplier_id, requester_name, cost_center_code, currency, po_date, status)
            VALUES (@Id, @number, @SupplierId, @RequesterName, @CostCenterCode, @Currency, @PoDate, 'Draft');
            SELECT @number;
            """,
            new { po.Id, po.SupplierId, po.RequesterName, po.CostCenterCode, po.Currency, PoDate = po.PoDate.Date },
            transaction);

        await connection.ExecuteAsync(
            """
            INSERT INTO dbo.po_line (po_id, line_no, item_code, description, uom, quantity, unit_price, gl_account, receipt_required)
            VALUES (@PoId, @LineNo, @ItemCode, @Description, @Uom, @Quantity, @UnitPrice, @GlAccount, @ReceiptRequired);
            """,
            po.Lines.Select(l => new
            {
                PoId = po.Id, l.LineNo, l.ItemCode, l.Description, l.Uom, l.Quantity, l.UnitPrice, l.GlAccount, l.ReceiptRequired,
            }),
            transaction);

        return number!;
    }

    /// <summary>Status, number and unrounded total of a PO, row-locked for the rest of the transaction.</summary>
    public static Task<ApprovalCandidateRow?> LockForApprovalAsync(
        SqlConnection connection, SqlTransaction transaction, Guid id) =>
        connection.QuerySingleOrDefaultAsync<ApprovalCandidateRow>(
            """
            SELECT po.po_number AS Number, po.status AS Status,
                   ISNULL((SELECT SUM(l.quantity * l.unit_price) FROM dbo.po_line l WHERE l.po_id = po.id), 0) AS Total
            FROM dbo.purchase_order po WITH (UPDLOCK, ROWLOCK)
            WHERE po.id = @id;
            """,
            new { id }, transaction);

    public static Task<int> MarkApprovedAsync(
        SqlConnection connection, SqlTransaction transaction, Guid id, string approverRole, string approvedBy) =>
        connection.ExecuteAsync(
            """
            UPDATE dbo.purchase_order
            SET status = 'Approved', approver_role = @approverRole, approved_by = @approvedBy, approved_at = SYSUTCDATETIME()
            WHERE id = @id AND status = 'Draft';
            """,
            new { id, approverRole, approvedBy }, transaction);
}
