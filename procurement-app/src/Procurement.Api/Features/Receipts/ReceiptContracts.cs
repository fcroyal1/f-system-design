namespace Procurement.Api.Features.Receipts;

/// <summary>POST /api/purchase-orders/{id}/receipts body. Fields are nullable so missing ones produce a readable 400.</summary>
public sealed record CreateReceiptRequest(
    string? ReceivedBy,
    string? PackingSlip,
    IReadOnlyList<CreateReceiptLineRequest?>? Lines);

public sealed record CreateReceiptLineRequest(int? PoLineNo, decimal? Quantity);

public sealed record ReceiptDto(
    Guid Id,
    string GrNumber,
    Guid PoId,
    string PoNumber,
    DateTime ReceivedAt,
    string ReceivedBy,
    string? PackingSlip,
    IReadOnlyList<ReceiptLineDto> Lines,
    IReadOnlyList<RematchedInvoiceDto> RematchedInvoices);

public sealed record ReceiptLineDto(int PoLineNo, decimal QuantityReceived);

/// <summary>A Blocked invoice on the PO that the receipt re-matched, with its new status.</summary>
public sealed record RematchedInvoiceDto(Guid Id, string InvoiceNumber, string? FromStatus, string Status, string? BlockReason);
