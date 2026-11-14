using System.Globalization;

namespace Procurement.Api.Features;

/// <summary>
/// Column limits shared by PO, invoice and receipt lines, checked up front so callers get a
/// readable 400 instead of a CHECK-constraint or overflow error.
/// </summary>
public static class Limits
{
    // quantity decimal(18,3), unit_price decimal(18,4).
    public const decimal MaxQuantity = 999_999_999_999_999.999m;
    public const decimal MaxUnitPrice = 99_999_999_999_999.9999m;

    // Line amounts and document totals must fit decimal(18,2); larger products of the column
    // maxima would also overflow C# decimal when read back, breaking every read of the document.
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public static string MaxAmountText => MaxAmount.ToString(CultureInfo.InvariantCulture);

    /// <summary>A quantity greater than 0 that fits decimal(18,3).</summary>
    public static decimal Quantity(decimal? value, string linePrefix)
    {
        if (value is not { } quantity)
        {
            throw new ValidationProblemException($"{linePrefix}: quantity is required.");
        }

        if (quantity <= 0)
        {
            throw new ValidationProblemException($"{linePrefix}: quantity must be greater than 0.");
        }

        if (quantity > MaxQuantity || HasMoreDecimals(quantity, 3))
        {
            throw new ValidationProblemException($"{linePrefix}: quantity must have at most 3 decimal places and 15 digits before the point.");
        }

        return quantity;
    }

    /// <summary>A unit price of 0 or more that fits decimal(18,4).</summary>
    public static decimal UnitPrice(decimal? value, string linePrefix)
    {
        if (value is not { } unitPrice)
        {
            throw new ValidationProblemException($"{linePrefix}: unitPrice is required.");
        }

        if (unitPrice < 0)
        {
            throw new ValidationProblemException($"{linePrefix}: unitPrice must be 0 or more.");
        }

        if (unitPrice > MaxUnitPrice || HasMoreDecimals(unitPrice, 4))
        {
            throw new ValidationProblemException($"{linePrefix}: unitPrice must have at most 4 decimal places and 14 digits before the point.");
        }

        return unitPrice;
    }

    /// <summary>Rejects a line whose quantity × unit price would not fit decimal(18,2).</summary>
    public static void LineAmount(decimal quantity, decimal unitPrice, string linePrefix)
    {
        // Compare against MaxAmount / unitPrice so the multiplication itself can never overflow.
        if (unitPrice > 0 && quantity > MaxAmount / unitPrice)
        {
            throw new ValidationProblemException($"{linePrefix}: amount exceeds the maximum of {MaxAmountText}.");
        }
    }

    /// <summary>
    /// Sums amounts that are each at most <see cref="MaxAmount"/>, failing as soon as the running
    /// total passes it, so the sum itself can never overflow.
    /// </summary>
    public static void Total(IEnumerable<decimal> amounts, string documentName)
    {
        var total = 0m;
        foreach (var amount in amounts)
        {
            total += amount;
            if (total > MaxAmount)
            {
                throw new ValidationProblemException($"The {documentName} total exceeds the maximum of {MaxAmountText}.");
            }
        }
    }

    public static bool HasMoreDecimals(decimal value, int places) => Math.Round(value, places) != value;
}
