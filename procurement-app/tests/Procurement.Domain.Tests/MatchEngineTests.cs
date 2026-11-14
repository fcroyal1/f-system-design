using Procurement.Domain;
using Procurement.Domain.Matching;

namespace Procurement.Domain.Tests;

public class MatchEngineTests
{
    private static MatchInput Build(
        decimal poQty = 100m, decimal poPrice = 10.00m, decimal? received = 98m, decimal previouslyInvoiced = 0m,
        decimal invQty = 100m, decimal invPrice = 10.20m, string item = "GLV-NIT-M", string uom = "BX",
        string invItem = "GLV-NIT-M", string invUom = "BX", int? poLineNo = 1, bool receiptRequired = true,
        string poCurrency = "USD", string invCurrency = "USD", PoStatus status = PoStatus.Approved,
        bool supplierMatches = true)
    {
        var recv = new Dictionary<int, decimal>();
        if (received is { } r) recv[1] = r;
        var prev = new Dictionary<int, decimal>();
        if (previouslyInvoiced != 0m) prev[1] = previouslyInvoiced;
        return new MatchInput(
            supplierMatches, poCurrency, invCurrency, status,
            [new PoLine(1, item, uom, poQty, poPrice, receiptRequired)],
            recv, prev,
            [new InvoiceLine(1, poLineNo, invItem, invUom, invQty, invPrice)]);
    }

    private static MatchInput GuideExample() => Build();

    private static MatchResult Run(MatchInput i) => MatchEngine.Match(i, Tolerance.Default);

    private static MatchCheck Check(MatchResult r, string name) => r.Checks.Single(c => c.Name == name);

    [Fact]
    public void Match_GuideExample_BlocksOnQuantityOnly()
    {
        var r = Run(GuideExample());
        Assert.False(r.Passed);
        Assert.Equal("Quantity: billed 2 more than received (line 1)", r.BlockReason);
        Assert.Equal(CheckOutcome.Pass, Check(r, "Unit price").Outcome);
        Assert.Equal("+2.0%, inside the ±3% / $50 price tolerance", Check(r, "Unit price").Message);
        Assert.Equal(CheckOutcome.Fail, Check(r, "Amount").Outcome);
        Assert.Equal("$40.00 more than the value received", Check(r, "Amount").Message);
        Assert.Equal(
            ["Supplier", "Currency", "PO status", "PO line", "Quantity", "Unit price", "Amount"],
            r.Checks.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void Match_GuideExample_CanonicalRows()
    {
        var r = Run(GuideExample());
        Assert.Equal(
            [
                new MatchCheck("Supplier", null, "—", "—", "—", CheckOutcome.Pass, "supplier matches the PO"),
                new MatchCheck("Currency", null, "USD", "—", "USD", CheckOutcome.Pass, "invoice and PO both in USD"),
                new MatchCheck("PO status", null, "Approved", "—", "—", CheckOutcome.Pass, "PO is approved"),
                new MatchCheck("PO line", 1, "GLV-NIT-M/BX", "—", "GLV-NIT-M/BX", CheckOutcome.Pass, "invoice line 1 matches PO line 1"),
                new MatchCheck("Quantity", 1, "100", "98", "100", CheckOutcome.Fail, "billed 2 more than received (line 1)"),
                new MatchCheck("Unit price", 1, "10.00", "—", "10.20", CheckOutcome.Pass, "+2.0%, inside the ±3% / $50 price tolerance"),
                new MatchCheck("Amount", 1, "1000.00", "980.00", "1020.00", CheckOutcome.Fail, "$40.00 more than the value received"),
            ],
            r.Checks.ToArray());
    }

    [Fact]
    public void Match_NorthlineExample_CanonicalRows()
    {
        var input = Build(poQty: 40m, poPrice: 6.50m, received: 40m, invQty: 40m, invPrice: 6.50m,
            item: "PPR-A4-500", uom: "RM", invItem: "PPR-A4-500", invUom: "RM");
        var r = Run(input);
        Assert.True(r.Passed);
        Assert.Equal(
            [
                new MatchCheck("Supplier", null, "—", "—", "—", CheckOutcome.Pass, "supplier matches the PO"),
                new MatchCheck("Currency", null, "USD", "—", "USD", CheckOutcome.Pass, "invoice and PO both in USD"),
                new MatchCheck("PO status", null, "Approved", "—", "—", CheckOutcome.Pass, "PO is approved"),
                new MatchCheck("PO line", 1, "PPR-A4-500/RM", "—", "PPR-A4-500/RM", CheckOutcome.Pass, "invoice line 1 matches PO line 1"),
                new MatchCheck("Quantity", 1, "40", "40", "40", CheckOutcome.Pass, "within the quantity received (line 1)"),
                new MatchCheck("Unit price", 1, "6.50", "—", "6.50", CheckOutcome.Pass, "+0.0%, inside the ±3% / $50 price tolerance"),
                new MatchCheck("Amount", 1, "260.00", "260.00", "260.00", CheckOutcome.Pass, "matches the value received"),
            ],
            r.Checks.ToArray());
    }

    [Fact]
    public void Match_FullyReceivedAtPoPrice_Passes()
    {
        var r = Run(Build(received: 100m, invPrice: 10.00m));
        Assert.True(r.Passed);
        Assert.Null(r.BlockReason);
    }

    [Fact]
    public void Match_PriceAtPercentLimit_Passes()
    {
        var r = Run(Build(poQty: 10m, poPrice: 100.00m, received: 10m, invQty: 10m, invPrice: 103.00m));
        Assert.True(r.Passed);
        Assert.Equal(CheckOutcome.Pass, Check(r, "Unit price").Outcome);
        Assert.Equal("+3.0%, inside the ±3% / $50 price tolerance", Check(r, "Unit price").Message);
    }

    [Fact]
    public void Match_PriceAtAbsoluteLimit_Passes()
    {
        var r = Run(Build(poQty: 1000m, poPrice: 10.00m, received: 1000m, invQty: 1000m, invPrice: 10.05m));
        Assert.Equal(CheckOutcome.Pass, Check(r, "Unit price").Outcome);
        Assert.True(r.Passed);
    }

    [Fact]
    public void Match_PriceOneCentOverAbsolute_Fails()
    {
        var r = Run(Build(poQty: 1000m, poPrice: 10.00m, received: 1000m, invQty: 1000m, invPrice: 10.0501m));
        Assert.Equal(CheckOutcome.Fail, Check(r, "Unit price").Outcome);
        Assert.Equal("+0.5% / $50.10 over the ±3% / $50 price tolerance", Check(r, "Unit price").Message);
    }

    [Fact]
    public void Match_PriceOverPercent_Fails()
    {
        var r = Run(Build(poQty: 1m, poPrice: 10.00m, received: 1m, invQty: 1m, invPrice: 10.31m));
        Assert.Equal(CheckOutcome.Fail, Check(r, "Unit price").Outcome);
        Assert.False(r.Passed);
    }

    [Fact]
    public void Match_Undercharge_Passes()
    {
        var r = Run(Build(received: 100m, invQty: 100m, invPrice: 9.50m));
        Assert.True(r.Passed);
    }

    [Fact]
    public void Match_SecondInvoiceCountsPreviouslyInvoiced()
    {
        var r = Run(Build(received: 98m, previouslyInvoiced: 60m, invQty: 40m, invPrice: 10.00m));
        Assert.Equal("Quantity: billed 2 more than received (line 1)", r.BlockReason);
    }

    [Fact]
    public void Match_TwoWayLine_UsesOrdered()
    {
        Assert.True(Run(Build(receiptRequired: false, received: null, invQty: 100m, invPrice: 10.00m)).Passed);
        var r = Run(Build(receiptRequired: false, received: null, invQty: 101m, invPrice: 10.00m));
        Assert.Equal("Quantity: billed 1 more than ordered (line 1)", r.BlockReason);
    }

    [Fact]
    public void Match_CurrencyMismatch_Fails()
    {
        var r = Run(Build(invCurrency: "EUR"));
        Assert.Equal("Currency: invoice in EUR, PO in USD", r.BlockReason);
    }

    [Fact]
    public void Match_DraftPo_Fails()
    {
        var r = Run(Build(status: PoStatus.Draft));
        Assert.Equal("PO status: PO is Draft, not approved", r.BlockReason);
    }

    [Fact]
    public void Match_SupplierMismatch_Fails()
    {
        var r = Run(Build(supplierMatches: false));
        Assert.Equal("Supplier: supplier on invoice is not the PO's supplier", r.BlockReason);
    }

    [Fact]
    public void Match_UnknownPoLine_Fails()
    {
        var r = Run(Build(poLineNo: 7));
        Assert.Equal("PO line: invoice line 1 has no matching PO line", r.BlockReason);
        Assert.Equal(4, r.Checks.Count);
    }

    [Fact]
    public void Match_ItemOrUomMismatch_Fails()
    {
        var r = Run(Build(invUom: "EA"));
        Assert.Equal("PO line: invoice line 1 is GLV-NIT-M/EA, PO line is GLV-NIT-M/BX", r.BlockReason);
        Assert.Equal(4, r.Checks.Count);
    }

    [Fact]
    public void Match_QuantityFailsWithUndercharge_AmountFails()
    {
        var r = Run(Build(received: 98m, invQty: 100m, invPrice: 9.50m));
        Assert.Equal("Quantity: billed 2 more than received (line 1)", r.BlockReason);
        var amount = Check(r, "Amount");
        Assert.Equal(CheckOutcome.Fail, amount.Outcome);
        Assert.Equal("fails with the quantity check", amount.Message);
    }

    [Fact]
    public void Match_PriceFailsQuantityOk_AmountFails()
    {
        var r = Run(Build(poQty: 1m, poPrice: 10.00m, received: 1m, invQty: 1m, invPrice: 10.31m));
        var amount = Check(r, "Amount");
        Assert.Equal(CheckOutcome.Fail, amount.Outcome);
        Assert.Equal("$0.31 more than the value received", amount.Message);
    }

    private static MatchInput TwoLinesOnPoLine1(decimal firstQty, decimal secondQty, decimal received = 98m) => new(
        true, "USD", "USD", PoStatus.Approved,
        [new PoLine(1, "GLV-NIT-M", "BX", 100m, 10.00m, true)],
        new Dictionary<int, decimal> { [1] = received },
        new Dictionary<int, decimal>(),
        [
            new InvoiceLine(1, 1, "GLV-NIT-M", "BX", firstQty, 10.00m),
            new InvoiceLine(2, 1, "GLV-NIT-M", "BX", secondQty, 10.00m),
        ]);

    [Fact]
    public void Match_SameInvoiceTwoLinesSamePoLine_CountsCumulatively()
    {
        var r = Run(TwoLinesOnPoLine1(98m, 98m));
        Assert.False(r.Passed);
        Assert.Equal("Quantity: billed 98 more than received (line 2)", r.BlockReason);
        Assert.Equal(CheckOutcome.Pass, r.Checks.Single(c => c.Name == "Quantity" && c.LineNo == 1).Outcome);
        var amount2 = r.Checks.Single(c => c.Name == "Amount" && c.LineNo == 2);
        Assert.Equal(CheckOutcome.Fail, amount2.Outcome);
        Assert.Equal("0.00", amount2.ReceiptValue);
    }

    [Fact]
    public void Match_SameInvoiceSplitLineWithinReceived_Passes()
    {
        var r = Run(TwoLinesOnPoLine1(50m, 48m));
        Assert.True(r.Passed);
        Assert.Equal("500.00", r.Checks.Single(c => c.Name == "Amount" && c.LineNo == 1).ReceiptValue);
        Assert.Equal("480.00", r.Checks.Single(c => c.Name == "Amount" && c.LineNo == 2).ReceiptValue);
    }

    [Fact]
    public void Match_FractionalOverbill_ShowsThreeDecimals()
    {
        var r = Run(Build(received: 100m, invQty: 100.004m, invPrice: 10.00m));
        Assert.Equal("Quantity: billed 0.004 more than received (line 1)", r.BlockReason);
    }
}
