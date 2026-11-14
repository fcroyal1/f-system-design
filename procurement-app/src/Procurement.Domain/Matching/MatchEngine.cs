using System.Globalization;

namespace Procurement.Domain.Matching;

public static class MatchEngine
{
    private const string NotApplicable = "—";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static MatchResult Match(MatchInput input, Tolerance tolerance)
    {
        var checks = new List<MatchCheck>();

        checks.Add(input.SupplierMatches
            ? new("Supplier", null, NotApplicable, NotApplicable, NotApplicable, CheckOutcome.Pass, "supplier matches the PO")
            : new("Supplier", null, NotApplicable, NotApplicable, NotApplicable, CheckOutcome.Fail, "supplier on invoice is not the PO's supplier"));

        var sameCurrency = string.Equals(input.PoCurrency, input.InvoiceCurrency, StringComparison.Ordinal);
        checks.Add(new("Currency", null, input.PoCurrency, NotApplicable, input.InvoiceCurrency,
            sameCurrency ? CheckOutcome.Pass : CheckOutcome.Fail,
            sameCurrency
                ? $"invoice and PO both in {input.PoCurrency}"
                : $"invoice in {input.InvoiceCurrency}, PO in {input.PoCurrency}"));

        var approved = input.PoStatus == PoStatus.Approved;
        checks.Add(new("PO status", null, input.PoStatus.ToString(), NotApplicable, NotApplicable,
            approved ? CheckOutcome.Pass : CheckOutcome.Fail,
            approved ? "PO is approved" : $"PO is {input.PoStatus}, not approved"));

        // Quantity already billed on each PO line by earlier lines of this invoice, so that lines
        // sharing a PO line count cumulatively (a split line is fine; billing it twice is not).
        var consumedByLine = new Dictionary<int, decimal>();
        foreach (var inv in input.InvoiceLines)
            AddLineChecks(checks, input, inv, tolerance, consumedByLine);

        return new MatchResult(checks);
    }

    private static void AddLineChecks(List<MatchCheck> checks, MatchInput input, InvoiceLine inv, Tolerance tol,
        Dictionary<int, decimal> consumedByLine)
    {
        var po = inv.PoLineNo is { } n ? input.PoLines.FirstOrDefault(l => l.LineNo == n) : null;
        var invKey = $"{inv.ItemCode}/{inv.Uom}";

        if (po is null)
        {
            checks.Add(new("PO line", inv.LineNo, NotApplicable, NotApplicable, invKey, CheckOutcome.Fail,
                $"invoice line {inv.LineNo} has no matching PO line"));
            return;
        }

        var poKey = $"{po.ItemCode}/{po.Uom}";
        if (!string.Equals(po.ItemCode, inv.ItemCode, StringComparison.Ordinal)
            || !string.Equals(po.Uom, inv.Uom, StringComparison.Ordinal))
        {
            checks.Add(new("PO line", inv.LineNo, poKey, NotApplicable, invKey, CheckOutcome.Fail,
                $"invoice line {inv.LineNo} is {invKey}, PO line is {poKey}"));
            return;
        }

        checks.Add(new("PO line", inv.LineNo, poKey, NotApplicable, invKey, CheckOutcome.Pass,
            $"invoice line {inv.LineNo} matches PO line {po.LineNo}"));

        // Quantity
        var received = input.ReceivedQtyByLine.GetValueOrDefault(po.LineNo);
        var previouslyInvoiced = input.PreviouslyInvoicedQtyByLine.GetValueOrDefault(po.LineNo);
        var basis = po.ReceiptRequired ? received : po.Quantity;
        var basisWord = po.ReceiptRequired ? "received" : "ordered";
        var consumed = consumedByLine.GetValueOrDefault(po.LineNo);
        consumedByLine[po.LineNo] = consumed + inv.Quantity;
        var available = Math.Max(0m, basis - previouslyInvoiced - consumed);
        var allowed = available * (1m + tol.QuantityPct / 100m);
        var qtyOk = inv.Quantity <= allowed;
        var over = inv.Quantity - available;
        checks.Add(new("Quantity", inv.LineNo, Qty(po.Quantity), po.ReceiptRequired ? Qty(received) : NotApplicable,
            Qty(inv.Quantity), qtyOk ? CheckOutcome.Pass : CheckOutcome.Fail,
            qtyOk
                ? $"within the quantity {basisWord} (line {inv.LineNo})"
                : $"billed {over.ToString("0.###", Inv)} more than {basisWord} (line {inv.LineNo})"));

        // Unit price: pct = (inv - po) / po * 100; abs = (inv - po) * invQty. Undercharges pass.
        var diff = inv.UnitPrice - po.UnitPrice;
        var abs = diff * inv.Quantity;
        var zeroBase = po.UnitPrice == 0m;
        var pct = zeroBase ? (diff == 0m ? 0m : 100m) : diff / po.UnitPrice * 100m;
        var priceOk = !(pct > tol.PricePct || abs > tol.PriceAbsolute);
        var pctText = pct.ToString("+0.0;-0.0;+0.0", Inv);
        checks.Add(new("Unit price", inv.LineNo, Price(po.UnitPrice), NotApplicable, Price(inv.UnitPrice),
            priceOk ? CheckOutcome.Pass : CheckOutcome.Fail,
            priceOk
                ? $"{pctText}%, inside the ±3% / $50 price tolerance"
                : $"{pctText}% / ${abs.ToString("0.00", Inv)} over the ±3% / $50 price tolerance"));

        // Amount fails exactly when the line's quantity or price check fails.
        var availableValue = Math.Min(inv.Quantity, available) * po.UnitPrice;
        var invAmount = inv.Quantity * inv.UnitPrice;
        var excess = invAmount - availableValue;
        var amountOk = qtyOk && priceOk;
        var amountMessage = amountOk ? "matches the value received"
            : excess > 0m ? $"${Money(excess)} more than the value received"
            : !qtyOk ? "fails with the quantity check"
            : "fails with the unit price check";
        checks.Add(new("Amount", inv.LineNo, Money(po.Quantity * po.UnitPrice), Money(availableValue), Money(invAmount),
            amountOk ? CheckOutcome.Pass : CheckOutcome.Fail, amountMessage));
    }

    private static string Qty(decimal v) => v.ToString("0.###", Inv);
    private static string Price(decimal v) => v.ToString("0.00##", Inv);
    private static string Money(decimal v) => v.ToString("0.00", Inv);
}
