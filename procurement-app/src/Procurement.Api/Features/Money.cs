namespace Procurement.Api.Features;

public static class Money
{
    /// <summary>Rounds an amount to cents for display only; rules always compare the unrounded value.</summary>
    public static decimal Display(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
