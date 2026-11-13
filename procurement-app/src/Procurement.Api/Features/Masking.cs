namespace Procurement.Api.Features;

/// <summary>Display form of a stored last-4 value: four bullets, a space, then the last 4.</summary>
public static class Masking
{
    public static string? Mask(string? last4) =>
        string.IsNullOrWhiteSpace(last4) ? null : "•••• " + last4.Trim();

    /// <summary>
    /// The last 4 letters or digits of a tax ID / bank account, ignoring separators. The full value
    /// is never stored. Returns null when nothing was given; throws 400 when fewer than 4 remain.
    /// </summary>
    public static string? Last4(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var characters = value.Where(char.IsAsciiLetterOrDigit).ToArray();
        if (characters.Length < 4)
        {
            throw new ValidationProblemException($"{fieldName} must contain at least 4 letters or digits.");
        }

        return new string(characters[^4..]).ToUpperInvariant();
    }
}
