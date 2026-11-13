using Procurement.Domain.Kpis;
using Xunit;

namespace Procurement.Domain.Tests;

public class KpiCalculatorTests
{
    [Fact]
    public void Calculate_Empty_AllZero() =>
        Assert.Equal(new KpiSnapshot(0m, 0m, 0m, 0m, 0, 0), KpiCalculator.Calculate([]));

    [Fact]
    public void Calculate_ExcludesExemptSpend()
    {
        var k = KpiCalculator.Calculate(
        [
            new(1020m, true, false, false, false, true),
            new(980m, false, false, null, false, false),
            new(5000m, false, true, null, false, false),
            new(2000m, true, false, true, true, false),
        ]);
        Assert.Equal(75.5m, k.PoComplianceBySpendPct);
        Assert.Equal(66.7m, k.PoComplianceByCountPct);
        Assert.Equal(50.0m, k.FirstPassMatchRatePct);
        Assert.Equal(50.0m, k.RetroPoRatePct);
        Assert.Equal(1, k.OpenExceptions);
        Assert.Equal(4, k.InvoiceCount);
    }

    [Fact]
    public void Calculate_SeedData()
    {
        var k = KpiCalculator.Calculate(
        [
            new(1020m, true, false, false, false, true),
            new(260m, true, false, true, false, false),
        ]);
        Assert.Equal(100.0m, k.PoComplianceBySpendPct);
        Assert.Equal(100.0m, k.PoComplianceByCountPct);
        Assert.Equal(50.0m, k.FirstPassMatchRatePct);
        Assert.Equal(0.0m, k.RetroPoRatePct);
        Assert.Equal(1, k.OpenExceptions);
        Assert.Equal(2, k.InvoiceCount);
    }
}
