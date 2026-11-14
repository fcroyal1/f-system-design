namespace Procurement.Api.Features.Receipts;

public static class ReceiptEndpoints
{
    public static IEndpointRouteBuilder MapReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/purchase-orders/{id:guid}/receipts",
                async (Guid id, CreateReceiptRequest request, ReceiptService service, CancellationToken cancellationToken) =>
                {
                    var receipt = await service.CreateAsync(id, request, cancellationToken);
                    // Receipts are shown on the PO detail, which is where Location points.
                    return Results.Created($"/api/purchase-orders/{id}", receipt);
                })
            .WithTags("Receipts");

        return app;
    }
}
