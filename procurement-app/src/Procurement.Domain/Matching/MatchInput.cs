namespace Procurement.Domain.Matching;

public record MatchInput(
    bool SupplierMatches,
    string PoCurrency,
    string InvoiceCurrency,
    PoStatus PoStatus,
    IReadOnlyList<PoLine> PoLines,
    IReadOnlyDictionary<int, decimal> ReceivedQtyByLine,
    IReadOnlyDictionary<int, decimal> PreviouslyInvoicedQtyByLine,
    IReadOnlyList<InvoiceLine> InvoiceLines);
