namespace Procurement.Api.Features;

public static class Validation
{
    /// <summary>A trimmed, non-blank string no longer than the column allows; otherwise a 400.</summary>
    public static string Required(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationProblemException($"{fieldName} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ValidationProblemException($"{fieldName} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    /// <summary>A 3-letter currency code, uppercased; otherwise a 400.</summary>
    public static string Currency(string? value)
    {
        var currency = Required(value, "currency", 3).ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
        {
            throw new ValidationProblemException("currency must be a 3-letter ISO code such as USD.");
        }

        return currency;
    }
}
