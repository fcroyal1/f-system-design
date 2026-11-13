using Procurement.Api.Data;

namespace Procurement.Api.Features.Reference;

public static class ReferenceEndpoints
{
    public static IEndpointRouteBuilder MapReferenceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reference", (ReferenceStore store, CancellationToken cancellationToken) =>
                store.GetAllAsync(cancellationToken))
            .WithTags("Reference");
        return app;
    }
}
