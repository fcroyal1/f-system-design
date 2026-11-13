namespace Procurement.Domain.Kpis;

public record InvoiceFact(decimal Total, bool HasPo, bool IsPoExemptCategory, bool? FirstMatchPassed, bool IsRetroPo, bool IsBlocked);

public record KpiSnapshot(
    decimal PoComplianceBySpendPct, decimal PoComplianceByCountPct,
    decimal FirstPassMatchRatePct, decimal RetroPoRatePct,
    int OpenExceptions, int InvoiceCount);

public static class KpiCalculator
{
    public static KpiSnapshot Calculate(IReadOnlyCollection<InvoiceFact> facts)
    {
        var addressable = facts.Where(f => !f.IsPoExemptCategory).ToList();
        var poBacked = facts.Where(f => f.HasPo).ToList();

        return new KpiSnapshot(
            Pct(addressable.Where(f => f.HasPo).Sum(f => f.Total), addressable.Sum(f => f.Total)),
            Pct(addressable.Count(f => f.HasPo), addressable.Count),
            Pct(poBacked.Count(f => f.FirstMatchPassed == true), poBacked.Count),
            Pct(poBacked.Count(f => f.IsRetroPo), poBacked.Count),
            facts.Count(f => f.IsBlocked),
            facts.Count);
    }

    private static decimal Pct(decimal numerator, decimal denominator) =>
        denominator == 0m ? 0m : Math.Round(numerator * 100m / denominator, 1, MidpointRounding.AwayFromZero);
}
