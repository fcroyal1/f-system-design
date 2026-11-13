namespace Procurement.Domain.Policies;

public static class InvoiceNumberNormalizer
{
    /// <summary>Uppercase, keep A-Z/0-9, drop the leading letter run, then leading zeros (all zeros -> "0").</summary>
    public static string Normalize(string raw)
    {
        var kept = new string((raw ?? "").ToUpperInvariant().Where(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9')).ToArray());
        var rest = kept.TrimStart('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                                  'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z');
        if (rest.Length == 0) return "";
        var noZeros = rest.TrimStart('0');
        return noZeros.Length == 0 ? "0" : noZeros;
    }
}
