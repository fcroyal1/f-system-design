using Procurement.Api.Data;

namespace Procurement.Api.Features.Suppliers;

public static class SupplierEndpoints
{
    public static IEndpointRouteBuilder MapSupplierEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/suppliers").WithTags("Suppliers");

        group.MapGet("/", async (SupplierStore store, CancellationToken cancellationToken) =>
            (await store.ListAsync(cancellationToken)).Select(SupplierDto.From).ToList());

        group.MapGet("/{id:guid}", (Guid id, SupplierService service, CancellationToken cancellationToken) =>
            service.GetAsync(id, cancellationToken));

        group.MapPost("/", async (CreateSupplierRequest request, SupplierService service, CancellationToken cancellationToken) =>
        {
            var supplier = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/suppliers/{supplier.Id}", supplier);
        });

        return app;
    }
}
