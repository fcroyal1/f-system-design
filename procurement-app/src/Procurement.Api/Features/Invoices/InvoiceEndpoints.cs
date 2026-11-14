namespace Procurement.Api.Features.Invoices;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invoices").WithTags("Invoices");

        group.MapGet("/", (string? status, InvoiceService service, CancellationToken cancellationToken) =>
            service.ListAsync(status, cancellationToken));

        group.MapGet("/{id:guid}", (Guid id, InvoiceService service, CancellationToken cancellationToken) =>
            service.GetAsync(id, cancellationToken));

        group.MapPost("/", async (CreateInvoiceRequest request, InvoiceService service, CancellationToken cancellationToken) =>
        {
            var invoice = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/invoices/{invoice.Id}", invoice);
        });

        group.MapPost("/{id:guid}/rematch", (Guid id, InvoiceService service, CancellationToken cancellationToken) =>
            service.RematchAsync(id, cancellationToken));

        return app;
    }
}
