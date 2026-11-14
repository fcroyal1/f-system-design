using System.Text.Json;
using System.Text.Json.Nodes;
using Procurement.Bff.Views;

namespace Procurement.Bff;

/// <summary>
/// Screen-shaped reads under /bff. Each one composes API GETs; none of them applies a business rule.
/// </summary>
public static class BffEndpoints
{
    public const string ApiUnavailableTitle = "The procurement API is unavailable";

    /// <summary>How many Blocked invoices the dashboard shows.</summary>
    public const int DashboardExceptionCount = 5;

    public static void MapBffEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff").AddEndpointFilter(TranslateApiFailures);

        group.MapGet("/dashboard", GetDashboard);
        group.MapGet("/purchase-orders/{id:guid}", GetPurchaseOrder);
        group.MapGet("/invoices/{id:guid}", GetInvoice);

        // Anything else under /bff is a 404 ProblemDetails, so it never reaches the SPA fallback.
        group.Map("/{**rest}", (HttpContext context) => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            detail: $"Nothing was found at {context.Request.Path}."));
    }

    private static async Task<IResult> GetDashboard(ApiClient api, CancellationToken cancellationToken)
    {
        var kpisTask = api.GetJsonAsync("api/kpis", cancellationToken);
        var blockedTask = api.GetJsonAsync("api/invoices?status=Blocked", cancellationToken);
        await Task.WhenAll(kpisTask, blockedTask);

        var blocked = Deserialize<List<InvoiceListItem>>(await blockedTask, "api/invoices?status=Blocked");
        var exceptions = blocked
            .OrderByDescending(i => i.InvoiceDate)
            .ThenBy(i => i.InvoiceNumber, StringComparer.Ordinal)
            .Take(DashboardExceptionCount)
            .Select(i => new ExceptionItem(i.Id, i.InvoiceNumber, i.SupplierName, i.Total, i.BlockReason))
            .ToList();

        return Results.Ok(new DashboardView(await kpisTask, exceptions));
    }

    private static async Task<IResult> GetPurchaseOrder(Guid id, ApiClient api, CancellationToken cancellationToken)
    {
        var po = await api.GetJsonAsync($"api/purchase-orders/{id}", cancellationToken);
        var supplierId = ReadGuid(po, "supplierId", "purchase order")
            ?? throw new ApiUnavailableException("The purchase order from the API has no supplierId.");
        var supplier = await api.GetJsonAsync($"api/suppliers/{supplierId}", cancellationToken);

        return Results.Ok(new PurchaseOrderView(po, supplier));
    }

    private static async Task<IResult> GetInvoice(Guid id, ApiClient api, CancellationToken cancellationToken)
    {
        var invoice = await api.GetJsonAsync($"api/invoices/{id}", cancellationToken);
        var supplierId = ReadGuid(invoice, "supplierId", "invoice")
            ?? throw new ApiUnavailableException("The invoice from the API has no supplierId.");
        var poId = ReadGuid(invoice, "poId", "invoice");

        // poId is null when the invoice was saved under "PO not found"; the view then has po = null.
        async Task<JsonNode?> LoadPo() =>
            poId is { } p ? await api.GetJsonAsync($"api/purchase-orders/{p}", cancellationToken) : null;

        var supplierTask = api.GetJsonAsync($"api/suppliers/{supplierId}", cancellationToken);
        var poTask = LoadPo();
        await Task.WhenAll((Task)supplierTask, poTask);

        return Results.Ok(new InvoiceView(invoice, await poTask, await supplierTask));
    }

    private static Guid? ReadGuid(JsonNode node, string property, string what)
    {
        var value = node[property];
        if (value is null)
        {
            return null;
        }

        try
        {
            return Guid.Parse(value.GetValue<string>());
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            throw new ApiUnavailableException($"The {what} from the API has an unreadable {property}.", ex);
        }
    }

    private static T Deserialize<T>(JsonNode node, string source)
    {
        try
        {
            return node.Deserialize<T>(JsonSerializerOptions.Web)
                ?? throw new ApiUnavailableException($"{source} returned null.");
        }
        catch (JsonException ex)
        {
            throw new ApiUnavailableException($"{source} returned an unexpected shape.", ex);
        }
    }

    /// <summary>Maps API failures on /bff calls to the BFF's error contract.</summary>
    private static async ValueTask<object?> TranslateApiFailures(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Procurement.Bff");
        try
        {
            return await next(context);
        }
        catch (ApiUnavailableException ex)
        {
            logger.LogWarning(ex, "API unavailable for {Path}", context.HttpContext.Request.Path);
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: ApiUnavailableTitle,
                detail: "The page could not be loaded because the procurement API did not respond. Try again shortly.");
        }
        catch (ApiNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Detail);
        }
        catch (ApiClientErrorException ex)
        {
            return Results.Content(ex.Body, ex.ContentType, statusCode: ex.Status);
        }
    }
}
