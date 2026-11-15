using Procurement.Api.Data;

namespace Procurement.Api.Features.Analytics;

public sealed record AgingBucketDto(string Bucket, int Count, decimal Amount);

public sealed record SupplierBaseDto(int Total, int Blocked, IReadOnlyList<SupplierCategoryRow> ByCategory);

public sealed record AnalyticsDto(
    IReadOnlyList<CategorySpendRow> SpendByCategory,
    IReadOnlyList<SupplierSpendRow> TopSuppliers,
    IReadOnlyList<StatusTotalRow> InvoicesByStatus,
    IReadOnlyList<BlockReasonRow> BlockReasons,
    IReadOnlyList<MonthRow> Monthly,
    IReadOnlyList<ApprovalRow> ApprovalsByRole,
    IReadOnlyList<AgingBucketDto> PayablesAging,
    SupplierBaseDto Suppliers);

public static class AnalyticsEndpoints
{
    /// <summary>Matched invoices by when they fall due, always in this order and always all four.</summary>
    private static readonly (string Name, Func<int, bool> Contains)[] Buckets =
    [
        ("Overdue", days => days < 0),
        ("Due in 0–30 days", days => days is >= 0 and <= 30),
        ("Due in 31–60 days", days => days is >= 31 and <= 60),
        ("Due after 60 days", days => days > 60),
    ];

    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/analytics", async (AnalyticsStore store, CancellationToken cancellationToken) =>
            {
                var rows = await store.ReadAsync(cancellationToken);

                var aging = Buckets
                    .Select(b =>
                    {
                        var due = rows.MatchedDue.Where(d => b.Contains(d.DaysUntilDue)).ToList();
                        return new AgingBucketDto(b.Name, due.Count, due.Sum(d => d.Amount));
                    })
                    .ToList();

                var suppliers = new SupplierBaseDto(
                    rows.SuppliersByCategory.Sum(c => c.Count),
                    rows.SuppliersByCategory.Sum(c => c.Blocked),
                    rows.SuppliersByCategory);

                return new AnalyticsDto(
                    rows.SpendByCategory, rows.TopSuppliers, rows.InvoicesByStatus, rows.BlockReasons,
                    rows.Monthly, rows.ApprovalsByRole, aging, suppliers);
            })
            .WithTags("Analytics");

        return app;
    }
}
