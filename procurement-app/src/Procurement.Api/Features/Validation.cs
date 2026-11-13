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
}
