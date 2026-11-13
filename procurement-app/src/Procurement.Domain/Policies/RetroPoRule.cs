namespace Procurement.Domain.Policies;

public static class RetroPoRule
{
    public static bool IsRetro(DateOnly invoiceDate, DateOnly poDate) => invoiceDate < poDate;
}
