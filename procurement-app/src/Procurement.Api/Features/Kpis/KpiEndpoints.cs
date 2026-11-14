using Procurement.Api.Data;
using Procurement.Domain.Kpis;

namespace Procurement.Api.Features.Kpis;

public static class KpiEndpoints
{
    public static IEndpointRouteBuilder MapKpiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/kpis", async (InvoiceStore store, CancellationToken cancellationToken) =>
            {
                var facts = await store.KpiFactsAsync(cancellationToken);
                return KpiCalculator.Calculate(facts
                    .Select(f => new InvoiceFact(f.Total, f.HasPo, f.IsPoExemptCategory, f.FirstMatchPassed, f.IsRetroPo, f.IsBlocked))
                    .ToList());
            })
            .WithTags("Kpis");

        return app;
    }
}
