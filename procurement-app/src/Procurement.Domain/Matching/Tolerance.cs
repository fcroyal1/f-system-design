namespace Procurement.Domain.Matching;

public record Tolerance(decimal QuantityPct, decimal PricePct, decimal PriceAbsolute)
{
    public static Tolerance Default { get; } = new(0m, 3.0m, 50.00m);
}
