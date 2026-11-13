namespace Procurement.Domain.Matching;

public record MatchCheck(
    string Name,
    int? LineNo,
    string PoValue,
    string ReceiptValue,
    string InvoiceValue,
    CheckOutcome Outcome,
    string Message);

public record MatchResult(IReadOnlyList<MatchCheck> Checks)
{
    public bool Passed => Checks.All(c => c.Outcome == CheckOutcome.Pass);

    public string? BlockReason =>
        Checks.FirstOrDefault(c => c.Outcome == CheckOutcome.Fail) is { } f ? $"{f.Name}: {f.Message}" : null;
}
