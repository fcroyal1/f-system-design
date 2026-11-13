namespace Procurement.Domain.Policies;

public record DoaTier(decimal MaxAmount, string ApproverRole);

public static class ApprovalRouter
{
    /// <summary>Approver role of the first tier (ascending MaxAmount) whose limit covers the total.</summary>
    public static string Route(decimal total, IReadOnlyList<DoaTier> tiers)
    {
        foreach (var tier in tiers.OrderBy(t => t.MaxAmount))
            if (total <= tier.MaxAmount) return tier.ApproverRole;
        throw new InvalidOperationException("No approval tier covers the invoice total.");
    }
}
