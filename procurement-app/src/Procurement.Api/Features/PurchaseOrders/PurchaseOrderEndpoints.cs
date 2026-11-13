using Procurement.Api.Data;

namespace Procurement.Api.Features.PurchaseOrders;

public static class PurchaseOrderEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/purchase-orders").WithTags("PurchaseOrders");

        group.MapGet("/", async (PurchaseOrderStore store, CancellationToken cancellationToken) =>
            (await store.ListAsync(cancellationToken)).Select(PurchaseOrderSummaryDto.From).ToList());

        group.MapGet("/{id:guid}", (Guid id, PurchaseOrderService service, CancellationToken cancellationToken) =>
            service.GetAsync(id, cancellationToken));

        group.MapPost("/", async (CreatePurchaseOrderRequest request, PurchaseOrderService service, CancellationToken cancellationToken) =>
        {
            var po = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/purchase-orders/{po.Id}", po);
        });

        group.MapPost("/{id:guid}/approve", (Guid id, PurchaseOrderService service, CancellationToken cancellationToken) =>
            service.ApproveAsync(id, cancellationToken));

        return app;
    }
}
