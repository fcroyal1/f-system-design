using Procurement.Api.Data;

namespace Procurement.Api.Features.PurchaseOrders;

/// <summary>POST /api/purchase-orders body. Fields are nullable so missing ones produce a readable 400.</summary>
public sealed record CreatePurchaseOrderRequest(
    Guid? SupplierId,
    string? RequesterName,
    string? CostCenterCode,
    string? Currency,
    DateOnly? PoDate,
    IReadOnlyList<CreatePoLineRequest?>? Lines);

public sealed record CreatePoLineRequest(
    string? ItemCode,
    string? Description,
    string? Uom,
    decimal? Quantity,
    decimal? UnitPrice,
    string? GlAccount,
    bool? ReceiptRequired);

public sealed record PurchaseOrderSummaryDto(
    Guid Id,
    string Number,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string RequesterName,
    string CostCenterCode,
    string Currency,
    DateOnly PoDate,
    string Status,
    string? ApproverRole,
    decimal Total)
{
    public static PurchaseOrderSummaryDto From(PurchaseOrderRow row) => new(
        row.Id, row.Number, row.SupplierId, row.SupplierCode, row.SupplierName, row.RequesterName, row.CostCenterCode,
        row.Currency, DateOnly.FromDateTime(row.PoDate), row.Status, row.ApproverRole, Money.Display(row.Total));
}

public sealed record PurchaseOrderDto(
    Guid Id,
    string Number,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string RequesterName,
    string CostCenterCode,
    string Currency,
    DateOnly PoDate,
    string Status,
    string? ApproverRole,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    decimal Total,
    IReadOnlyList<PoLineDto> Lines,
    IReadOnlyList<ReceiptSummaryDto> Receipts,
    IReadOnlyList<InvoiceSummaryDto> Invoices)
{
    public static PurchaseOrderDto From(PurchaseOrderDetailRows rows)
    {
        var h = rows.Header;
        return new PurchaseOrderDto(
            h.Id, h.Number, h.SupplierId, h.SupplierCode, h.SupplierName, h.RequesterName, h.CostCenterCode, h.Currency,
            DateOnly.FromDateTime(h.PoDate), h.Status, h.ApproverRole, h.ApprovedBy, h.ApprovedAt, Money.Display(h.Total),
            rows.Lines.Select(l => new PoLineDto(
                l.LineNo, l.ItemCode, l.Description, l.Uom, l.Quantity, l.UnitPrice, l.GlAccount, l.ReceiptRequired,
                Money.Display(l.Quantity * l.UnitPrice))).ToList(),
            rows.Receipts.Select(r => new ReceiptSummaryDto(
                r.Id, r.GrNumber, r.ReceivedAt, r.ReceivedBy, r.PackingSlip, r.QuantityReceived)).ToList(),
            rows.Invoices.Select(i => new InvoiceSummaryDto(
                i.Id, i.InvoiceNumber, DateOnly.FromDateTime(i.InvoiceDate), DateOnly.FromDateTime(i.DueDate), i.Currency,
                i.Status, i.BlockReason, i.IsRetroPo, Money.Display(i.Total))).ToList());
    }
}

public sealed record PoLineDto(
    int LineNo,
    string ItemCode,
    string Description,
    string Uom,
    decimal Quantity,
    decimal UnitPrice,
    string GlAccount,
    bool ReceiptRequired,
    decimal Amount);

public sealed record ReceiptSummaryDto(
    Guid Id,
    string GrNumber,
    DateTime ReceivedAt,
    string ReceivedBy,
    string? PackingSlip,
    decimal QuantityReceived);

public sealed record InvoiceSummaryDto(
    Guid Id,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string Currency,
    string Status,
    string? BlockReason,
    bool IsRetroPo,
    decimal Total);
