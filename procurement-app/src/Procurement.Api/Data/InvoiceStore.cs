using Dapper;
using Microsoft.Data.SqlClient;
using Procurement.Domain.Matching;

namespace Procurement.Api.Data;

/// <summary>An invoice header as listed and shown, with its supplier and PO.</summary>
public sealed class InvoiceRow
{
    public Guid Id { get; init; }
    public string InvoiceNumber { get; init; } = "";
    public string NormalizedNumber { get; init; } = "";
    public Guid SupplierId { get; init; }
    public string SupplierCode { get; init; } = "";
    public string SupplierName { get; init; } = "";
    public DateTime InvoiceDate { get; init; }
    public DateTime DueDate { get; init; }
    public string Currency { get; init; } = "";
    public Guid? PoId { get; init; }
    public string? PoNumber { get; init; }
    public decimal TaxAmount { get; init; }
    public string Status { get; init; } = "";
    public string? BlockReason { get; init; }
    public bool? FirstMatchPassed { get; init; }
    public bool IsRetroPo { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>Σ line quantity × unit_price, unrounded.</summary>
    public decimal Subtotal { get; init; }
}

public sealed class InvoiceLineRow
{
    public int LineNo { get; init; }
    public int? PoLineNo { get; init; }
    public string ItemCode { get; init; } = "";
    public string Uom { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}

public sealed class MatchCheckRow
{
    public int Seq { get; init; }
    public string Name { get; init; } = "";
    public int? LineNo { get; init; }
    public string PoValue { get; init; } = "";
    public string ReceiptValue { get; init; } = "";
    public string InvoiceValue { get; init; } = "";
    public string Outcome { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed record InvoiceDetailRows(InvoiceRow Header, IReadOnlyList<InvoiceLineRow> Lines, IReadOnlyList<MatchCheckRow> Checks);

/// <summary>The PO side of a match, read under an update lock.</summary>
public sealed class MatchPoRow
{
    public Guid Id { get; init; }
    public string Number { get; init; } = "";
    public Guid SupplierId { get; init; }
    public string Currency { get; init; } = "";
    public DateTime PoDate { get; init; }
    public string Status { get; init; } = "";
}

/// <summary>The invoice side of a match, read under an update lock.</summary>
public sealed class MatchInvoiceRow
{
    public Guid Id { get; init; }
    public string InvoiceNumber { get; init; } = "";
    public Guid SupplierId { get; init; }
    public string SupplierCode { get; init; } = "";
    public bool SupplierBlocked { get; init; }
    public Guid? PoId { get; init; }
    public string Currency { get; init; } = "";
    public string Status { get; init; } = "";
}

public sealed class MatchPoLineRow
{
    public int LineNo { get; init; }
    public string ItemCode { get; init; } = "";
    public string Uom { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public bool ReceiptRequired { get; init; }
}

public sealed class LineQuantityRow
{
    public int LineNo { get; init; }
    public decimal Quantity { get; init; }
}

public sealed class InvoiceFactRow
{
    public decimal Total { get; init; }
    public bool HasPo { get; init; }
    public bool IsPoExemptCategory { get; init; }
    public bool? FirstMatchPassed { get; init; }
    public bool IsRetroPo { get; init; }
    public bool IsBlocked { get; init; }
}

public sealed record NewInvoiceLine(
    int LineNo, int? PoLineNo, string ItemCode, string Uom, string Description, decimal Quantity, decimal UnitPrice);

public sealed record NewInvoice(
    Guid Id, Guid SupplierId, string InvoiceNumber, string NormalizedNumber, DateTime InvoiceDate, DateTime DueDate,
    string Currency, Guid? PoId, decimal TaxAmount, string? BlockReason, bool IsRetroPo, IReadOnlyList<NewInvoiceLine> Lines);

public sealed class InvoiceStore(Db db)
{
    private const string SelectHeader =
        """
        SELECT i.id AS Id, i.invoice_number AS InvoiceNumber, i.normalized_number AS NormalizedNumber,
               i.supplier_id AS SupplierId, s.code AS SupplierCode, s.legal_name AS SupplierName,
               i.invoice_date AS InvoiceDate, i.due_date AS DueDate, i.currency AS Currency,
               i.po_id AS PoId, po.po_number AS PoNumber, i.tax_amount AS TaxAmount, i.status AS Status,
               i.block_reason AS BlockReason, i.first_match_passed AS FirstMatchPassed, i.is_retro_po AS IsRetroPo,
               i.created_at AS CreatedAt,
               ISNULL((SELECT SUM(il.quantity * il.unit_price) FROM dbo.invoice_line il WHERE il.invoice_id = i.id), 0) AS Subtotal
        FROM dbo.invoice i
        JOIN dbo.supplier s ON s.id = i.supplier_id
        LEFT JOIN dbo.purchase_order po ON po.id = i.po_id
        """;

    public async Task<IReadOnlyList<InvoiceRow>> ListAsync(string? status, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<InvoiceRow>(new CommandDefinition(
            SelectHeader + " WHERE @status IS NULL OR i.status = @status ORDER BY i.invoice_date DESC, i.invoice_number;",
            new { status }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<InvoiceDetailRows?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenAsync(cancellationToken);
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            SelectHeader + " WHERE i.id = @id;" +
            """

            SELECT line_no AS [LineNo], po_line_no AS PoLineNo, item_code AS ItemCode, uom AS Uom,
                   description AS Description, quantity AS Quantity, unit_price AS UnitPrice
            FROM dbo.invoice_line WHERE invoice_id = @id ORDER BY line_no;

            SELECT seq AS Seq, name AS Name, line_no AS [LineNo], po_value AS PoValue, receipt_value AS ReceiptValue,
                   invoice_value AS InvoiceValue, outcome AS Outcome, message AS Message
            FROM dbo.match_check WHERE invoice_id = @id ORDER BY seq;
            """,
            new { id }, cancellationToken: cancellationToken));

        var header = await results.ReadSingleOrDefaultAsync<InvoiceRow>();
        if (header is null)
        {
            return null;
        }

        var lines = (await results.ReadAsync<InvoiceLineRow>()).ToList();
        var checks = (await results.ReadAsync<MatchCheckRow>()).ToList();
        return new InvoiceDetailRows(header, lines, checks);
    }

    /// <summary>One fact per invoice for the KPI calculator; the spend category is the supplier's.</summary>
    public async Task<IReadOnlyList<InvoiceFactRow>> KpiFactsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<InvoiceFactRow>(new CommandDefinition(
            """
            SELECT ISNULL((SELECT SUM(il.quantity * il.unit_price) FROM dbo.invoice_line il WHERE il.invoice_id = i.id), 0)
                       + i.tax_amount AS Total,
                   CAST(CASE WHEN i.po_id IS NULL THEN 0 ELSE 1 END AS bit) AS HasPo,
                   c.is_po_exempt AS IsPoExemptCategory,
                   i.first_match_passed AS FirstMatchPassed,
                   i.is_retro_po AS IsRetroPo,
                   CAST(CASE WHEN i.status = 'Blocked' THEN 1 ELSE 0 END AS bit) AS IsBlocked
            FROM dbo.invoice i
            JOIN dbo.supplier s ON s.id = i.supplier_id
            JOIN dbo.spend_category c ON c.code = s.category_code;
            """, cancellationToken: cancellationToken))).ToList();
    }

    /// <summary>
    /// The PO with this number, update-locked (and range-locked when absent) for the rest of the
    /// transaction. Receipts and matches lock the PO row first, so they serialize per PO.
    /// </summary>
    public static Task<MatchPoRow?> LockPoByNumberAsync(SqlConnection connection, SqlTransaction transaction, string number) =>
        connection.QuerySingleOrDefaultAsync<MatchPoRow>(
            SelectMatchPo + " WHERE po_number = @number;", new { number }, transaction);

    public static Task<MatchPoRow?> LockPoAsync(SqlConnection connection, SqlTransaction transaction, Guid id) =>
        connection.QuerySingleOrDefaultAsync<MatchPoRow>(
            SelectMatchPo + " WHERE id = @id;", new { id }, transaction);

    private const string SelectMatchPo =
        """
        SELECT id AS Id, po_number AS Number, supplier_id AS SupplierId, currency AS Currency, po_date AS PoDate, status AS Status
        FROM dbo.purchase_order WITH (UPDLOCK, HOLDLOCK)
        """;

    /// <summary>
    /// The raw number of the supplier's invoice with this normalized number, or null. Range-locked
    /// inside a transaction so a concurrent insert of the same number waits.
    /// </summary>
    public static Task<string?> FindNumberAsync(
        SqlConnection connection, SqlTransaction? transaction, Guid supplierId, string normalizedNumber) =>
        connection.QuerySingleOrDefaultAsync<string?>(
            """
            SELECT invoice_number FROM dbo.invoice WITH (UPDLOCK, HOLDLOCK)
            WHERE supplier_id = @supplierId AND normalized_number = @normalizedNumber;
            """,
            new { supplierId, normalizedNumber }, transaction);

    public static async Task InsertAsync(SqlConnection connection, SqlTransaction transaction, NewInvoice invoice)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO dbo.invoice (id, supplier_id, invoice_number, normalized_number, invoice_date, due_date, currency,
                                     po_id, tax_amount, status, block_reason, first_match_passed, is_retro_po)
            VALUES (@Id, @SupplierId, @InvoiceNumber, @NormalizedNumber, @InvoiceDate, @DueDate, @Currency,
                    @PoId, @TaxAmount, 'Blocked', @BlockReason, NULL, @IsRetroPo);
            """,
            invoice, transaction);

        await connection.ExecuteAsync(
            """
            INSERT INTO dbo.invoice_line (invoice_id, line_no, po_line_no, item_code, uom, description, quantity, unit_price)
            VALUES (@InvoiceId, @LineNo, @PoLineNo, @ItemCode, @Uom, @Description, @Quantity, @UnitPrice);
            """,
            invoice.Lines.Select(l => new
            {
                InvoiceId = invoice.Id, l.LineNo, l.PoLineNo, l.ItemCode, l.Uom, l.Description, l.Quantity, l.UnitPrice,
            }),
            transaction);
    }

    /// <summary>The PO id an invoice points at, read without a lock (the caller locks the PO next).</summary>
    public static Task<(bool Exists, Guid? PoId)> FindPoIdAsync(SqlConnection connection, SqlTransaction transaction, Guid id) =>
        connection.QuerySingleOrDefaultAsync<(bool, Guid?)>(
            "SELECT CAST(1 AS bit), po_id FROM dbo.invoice WHERE id = @id;", new { id }, transaction);

    public static Task<MatchInvoiceRow> LockInvoiceAsync(SqlConnection connection, SqlTransaction transaction, Guid id) =>
        connection.QuerySingleAsync<MatchInvoiceRow>(
            """
            SELECT i.id AS Id, i.invoice_number AS InvoiceNumber, i.supplier_id AS SupplierId, s.code AS SupplierCode,
                   s.is_blocked AS SupplierBlocked, i.po_id AS PoId, i.currency AS Currency, i.status AS Status
            FROM dbo.invoice i WITH (UPDLOCK)
            JOIN dbo.supplier s ON s.id = i.supplier_id
            WHERE i.id = @id;
            """,
            new { id }, transaction);

    public static async Task<IReadOnlyList<MatchPoLineRow>> PoLinesAsync(SqlConnection connection, SqlTransaction transaction, Guid poId) =>
        (await connection.QueryAsync<MatchPoLineRow>(
            """
            SELECT line_no AS [LineNo], item_code AS ItemCode, uom AS Uom, quantity AS Quantity, unit_price AS UnitPrice,
                   receipt_required AS ReceiptRequired
            FROM dbo.po_line WHERE po_id = @poId ORDER BY line_no;
            """,
            new { poId }, transaction)).ToList();

    /// <summary>Σ received per PO line across all goods receipts on the PO.</summary>
    public static async Task<IReadOnlyDictionary<int, decimal>> ReceivedByLineAsync(
        SqlConnection connection, SqlTransaction transaction, Guid poId) =>
        (await connection.QueryAsync<LineQuantityRow>(
            """
            SELECT gl.po_line_no AS [LineNo], SUM(gl.quantity_received) AS Quantity
            FROM dbo.gr_line gl JOIN dbo.goods_receipt gr ON gr.id = gl.gr_id
            WHERE gr.po_id = @poId
            GROUP BY gl.po_line_no;
            """,
            new { poId }, transaction)).ToDictionary(r => r.LineNo, r => r.Quantity);

    /// <summary>Σ invoiced per PO line on the PO's other Matched invoices (Blocked ones never count).</summary>
    public static async Task<IReadOnlyDictionary<int, decimal>> PreviouslyInvoicedByLineAsync(
        SqlConnection connection, SqlTransaction transaction, Guid poId, Guid excludeInvoiceId) =>
        (await connection.QueryAsync<LineQuantityRow>(
            """
            SELECT il.po_line_no AS [LineNo], SUM(il.quantity) AS Quantity
            FROM dbo.invoice_line il JOIN dbo.invoice i ON i.id = il.invoice_id
            WHERE i.po_id = @poId AND i.status = 'Matched' AND i.id <> @excludeInvoiceId AND il.po_line_no IS NOT NULL
            GROUP BY il.po_line_no;
            """,
            new { poId, excludeInvoiceId }, transaction)).ToDictionary(r => r.LineNo, r => r.Quantity);

    public static async Task<IReadOnlyList<InvoiceLineRow>> LinesAsync(SqlConnection connection, SqlTransaction transaction, Guid invoiceId) =>
        (await connection.QueryAsync<InvoiceLineRow>(
            """
            SELECT line_no AS [LineNo], po_line_no AS PoLineNo, item_code AS ItemCode, uom AS Uom,
                   description AS Description, quantity AS Quantity, unit_price AS UnitPrice
            FROM dbo.invoice_line WHERE invoice_id = @invoiceId ORDER BY line_no;
            """,
            new { invoiceId }, transaction)).ToList();

    /// <summary>Deletes the invoice's check rows and inserts these as seq 1..n.</summary>
    public static async Task ReplaceChecksAsync(
        SqlConnection connection, SqlTransaction transaction, Guid invoiceId, IReadOnlyList<MatchCheck> checks)
    {
        await connection.ExecuteAsync(
            "DELETE FROM dbo.match_check WHERE invoice_id = @invoiceId;", new { invoiceId }, transaction);

        await connection.ExecuteAsync(
            """
            INSERT INTO dbo.match_check (invoice_id, seq, name, line_no, po_value, receipt_value, invoice_value, outcome, message)
            VALUES (@InvoiceId, @Seq, @Name, @LineNo, @PoValue, @ReceiptValue, @InvoiceValue, @Outcome, @Message);
            """,
            checks.Select((c, i) => new
            {
                InvoiceId = invoiceId, Seq = i + 1, c.Name, c.LineNo, c.PoValue, c.ReceiptValue, c.InvoiceValue,
                Outcome = c.Outcome.ToString(), c.Message,
            }),
            transaction);
    }

    /// <summary>
    /// Sets status and reason. <c>first_match_passed</c> is written only while it is NULL, so the
    /// first match's result is kept; pass null when no match was run.
    /// </summary>
    public static Task SetOutcomeAsync(
        SqlConnection connection, SqlTransaction transaction, Guid id, string status, string? blockReason, bool? matchPassed) =>
        connection.ExecuteAsync(
            """
            UPDATE dbo.invoice
            SET status = @status, block_reason = @blockReason,
                first_match_passed = COALESCE(first_match_passed, @matchPassed)
            WHERE id = @id;
            """,
            new { id, status, blockReason, matchPassed }, transaction);

    public static async Task<IReadOnlyList<Guid>> BlockedIdsForPoAsync(SqlConnection connection, SqlTransaction transaction, Guid poId) =>
        (await connection.QueryAsync<Guid>(
            """
            SELECT id FROM dbo.invoice
            WHERE po_id = @poId AND status = 'Blocked'
            ORDER BY invoice_date, created_at, id;
            """,
            new { poId }, transaction)).ToList();
}
