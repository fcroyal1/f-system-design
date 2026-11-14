using Procurement.Api.Data;

namespace Procurement.Api.Features.Invoices;

/// <summary>POST /api/invoices body. Fields are nullable so missing ones produce a readable 400.</summary>
public sealed record CreateInvoiceRequest(
    Guid? SupplierId,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    string? Currency,
    string? PoNumber,
    decimal? TaxAmount,
    IReadOnlyList<CreateInvoiceLineRequest?>? Lines);

public sealed record CreateInvoiceLineRequest(
    int? PoLineNo,
    string? ItemCode,
    string? Uom,
    string? Description,
    decimal? Quantity,
    decimal? UnitPrice);

public sealed record InvoiceListItemDto(
    Guid Id,
    string InvoiceNumber,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    Guid? PoId,
    string? PoNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string Currency,
    string Status,
    string? BlockReason,
    bool IsRetroPo,
    decimal Total)
{
    public static InvoiceListItemDto From(InvoiceRow row) => new(
        row.Id, row.InvoiceNumber, row.SupplierId, row.SupplierCode, row.SupplierName, row.PoId, row.PoNumber,
        DateOnly.FromDateTime(row.InvoiceDate), DateOnly.FromDateTime(row.DueDate), row.Currency, row.Status,
        row.BlockReason, row.IsRetroPo, Money.Display(row.Subtotal + row.TaxAmount));
}

public sealed record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    string NormalizedNumber,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    Guid? PoId,
    string? PoNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    string Currency,
    string Status,
    string? BlockReason,
    bool? FirstMatchPassed,
    bool IsRetroPo,
    DateTime CreatedAt,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total,
    IReadOnlyList<InvoiceLineDto> Lines,
    IReadOnlyList<MatchCheckDto> Checks)
{
    public static InvoiceDto From(InvoiceDetailRows rows)
    {
        var h = rows.Header;
        return new InvoiceDto(
            h.Id, h.InvoiceNumber, h.NormalizedNumber, h.SupplierId, h.SupplierCode, h.SupplierName, h.PoId, h.PoNumber,
            DateOnly.FromDateTime(h.InvoiceDate), DateOnly.FromDateTime(h.DueDate), h.Currency, h.Status, h.BlockReason,
            h.FirstMatchPassed, h.IsRetroPo, h.CreatedAt,
            Money.Display(h.Subtotal), h.TaxAmount, Money.Display(h.Subtotal + h.TaxAmount),
            rows.Lines.Select(l => new InvoiceLineDto(
                l.LineNo, l.PoLineNo, l.ItemCode, l.Uom, l.Description, l.Quantity, l.UnitPrice,
                Money.Display(l.Quantity * l.UnitPrice))).ToList(),
            rows.Checks.Select(c => new MatchCheckDto(
                c.Seq, c.Name, c.LineNo, c.PoValue, c.ReceiptValue, c.InvoiceValue, c.Outcome, c.Message)).ToList());
    }
}

public sealed record InvoiceLineDto(
    int LineNo,
    int? PoLineNo,
    string ItemCode,
    string Uom,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount);

public sealed record MatchCheckDto(
    int Seq,
    string Name,
    int? LineNo,
    string PoValue,
    string ReceiptValue,
    string InvoiceValue,
    string Outcome,
    string Message);

/// <summary>What one (re-)evaluation did to an invoice.</summary>
public sealed record InvoiceOutcome(Guid Id, string InvoiceNumber, string? FromStatus, string Status, string? BlockReason);
