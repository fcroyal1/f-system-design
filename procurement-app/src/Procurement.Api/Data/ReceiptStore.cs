using Dapper;
using Microsoft.Data.SqlClient;

namespace Procurement.Api.Data;

public sealed class ReceiptPoRow
{
    public string Number { get; init; } = "";
    public string Status { get; init; } = "";
}

public sealed class InsertedReceiptRow
{
    public string GrNumber { get; init; } = "";
    public DateTime ReceivedAt { get; init; }
}

public sealed record NewReceiptLine(int PoLineNo, decimal Quantity);

public sealed record NewReceipt(Guid Id, Guid PoId, string ReceivedBy, string? PackingSlip, IReadOnlyList<NewReceiptLine> Lines);

public static class ReceiptStore
{
    /// <summary>
    /// The PO's number and status, update-locked for the rest of the transaction, so concurrent
    /// receipts (and matches) on one PO run one after the other.
    /// </summary>
    public static Task<ReceiptPoRow?> LockPoAsync(SqlConnection connection, SqlTransaction transaction, Guid poId) =>
        connection.QuerySingleOrDefaultAsync<ReceiptPoRow>(
            "SELECT po_number AS Number, status AS Status FROM dbo.purchase_order WITH (UPDLOCK, HOLDLOCK) WHERE id = @poId;",
            new { poId }, transaction);

    /// <summary>Ordered quantity per PO line, with the lines update-locked before anything is summed.</summary>
    public static async Task<IReadOnlyDictionary<int, decimal>> LockOrderedByLineAsync(
        SqlConnection connection, SqlTransaction transaction, Guid poId) =>
        (await connection.QueryAsync<LineQuantityRow>(
            "SELECT line_no AS [LineNo], quantity AS Quantity FROM dbo.po_line WITH (UPDLOCK, HOLDLOCK) WHERE po_id = @poId;",
            new { poId }, transaction)).ToDictionary(r => r.LineNo, r => r.Quantity);

    /// <summary>Inserts the goods receipt (numbered from <c>gr_number_seq</c>, received now, UTC) and its lines.</summary>
    public static async Task<InsertedReceiptRow> InsertAsync(SqlConnection connection, SqlTransaction transaction, NewReceipt receipt)
    {
        var inserted = await connection.QuerySingleAsync<InsertedReceiptRow>(
            """
            INSERT INTO dbo.goods_receipt (id, gr_number, po_id, received_at, received_by, packing_slip)
            OUTPUT inserted.gr_number AS GrNumber, inserted.received_at AS ReceivedAt
            VALUES (@Id, CAST(NEXT VALUE FOR dbo.gr_number_seq AS varchar(10)), @PoId, SYSUTCDATETIME(), @ReceivedBy, @PackingSlip);
            """,
            new { receipt.Id, receipt.PoId, receipt.ReceivedBy, receipt.PackingSlip }, transaction);

        await connection.ExecuteAsync(
            "INSERT INTO dbo.gr_line (gr_id, po_line_no, quantity_received) VALUES (@GrId, @PoLineNo, @Quantity);",
            receipt.Lines.Select(l => new { GrId = receipt.Id, l.PoLineNo, l.Quantity }), transaction);

        return inserted;
    }
}
