using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Procurement.Bff.Tests;

/// <summary>A request as FakeApi received it, so tests can prove the BFF forwarded it unchanged.</summary>
public sealed record ReceivedRequest(string Method, string Path, string QueryString, string ContentType, string Body);

/// <summary>
/// An in-process Kestrel app on a random loopback port that answers like Procurement.Api with canned JSON.
/// It knows nothing about the real API assembly: the shapes are copied from the Task 4 and Task 5 contracts.
/// </summary>
public sealed class FakeApi : IAsyncDisposable
{
    public static readonly Guid SupplierId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid PoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid InvoiceId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // Invoice ids that make FakeApi misbehave in a specific way.
    public static readonly Guid Invoice503Id = Guid.Parse("50300000-0000-0000-0000-000000000000");
    public static readonly Guid Invoice400Id = Guid.Parse("40000000-0000-0000-0000-000000000000");
    public static readonly Guid InvoiceStallId = Guid.Parse("57a11000-0000-0000-0000-000000000000");
    public static readonly Guid InvoiceCutOffId = Guid.Parse("c0700000-0000-0000-0000-000000000000");
    public const string Invoice400Detail = "status must be Matched or Blocked.";

    private readonly WebApplication _app;

    public ConcurrentQueue<ReceivedRequest> Received { get; } = new();

    public string BaseUrl { get; }

    private FakeApi(WebApplication app, string baseUrl)
    {
        _app = app;
        BaseUrl = baseUrl;
    }

    public static async Task<FakeApi> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();

        FakeApi? self = null;
        Map(app, () => self!);

        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        self = new FakeApi(app, address);
        return self;
    }

    private static void Map(WebApplication app, Func<FakeApi> self)
    {
        app.MapGet("/api/kpis", () => Results.Text(Kpis, "application/json"));

        app.MapGet("/api/invoices", (string? status) =>
        {
            var list = InvoiceList()
                .Where(i => status is null || string.Equals((string?)i!["status"], status, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return Results.Text(new JsonArray(list).ToJsonString(), "application/json");
        });

        app.MapGet("/api/invoices/{id:guid}", async (Guid id, HttpContext ctx) =>
        {
            if (id == InvoiceId)
            {
                return Results.Text(InvoiceDetail, "application/json");
            }

            if (id == Invoice503Id)
            {
                return Results.Text(Problem(503, "Service Unavailable", "The database is unavailable.", ctx.Request.Path),
                    "application/problem+json", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (id == Invoice400Id)
            {
                return Results.Text(Problem(400, "Bad Request", Invoice400Detail, ctx.Request.Path),
                    "application/problem+json", statusCode: StatusCodes.Status400BadRequest);
            }

            if (id == InvoiceStallId || id == InvoiceCutOffId)
            {
                // Headers and the start of a body go out, then the body either stalls or the connection drops.
                ctx.Response.StatusCode = StatusCodes.Status200OK;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength = 4096;
                await ctx.Response.Body.WriteAsync("{\"id\":\""u8.ToArray(), ctx.RequestAborted);
                await ctx.Response.Body.FlushAsync(ctx.RequestAborted);

                if (id == InvoiceStallId)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), ctx.RequestAborted);
                    }
                    catch (OperationCanceledException)
                    {
                        // The BFF gave up, which is what the test expects.
                    }
                }

                ctx.Abort();
                return Results.Empty;
            }

            return NotFound(ctx, $"Invoice {id} was not found.");
        });

        app.MapGet("/api/purchase-orders/{id:guid}", (Guid id, HttpContext ctx) =>
            id == PoId ? Results.Text(PurchaseOrderDetail, "application/json") : NotFound(ctx, $"Purchase order {id} was not found."));

        app.MapGet("/api/suppliers/{id:guid}", (Guid id, HttpContext ctx) =>
            id == SupplierId ? Results.Text(Supplier, "application/json") : NotFound(ctx, $"Supplier {id} was not found."));

        // Echo: records what arrived and returns it, so the pass-through test can compare both ends.
        app.MapPost("/api/invoices", async (HttpContext ctx) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();
            var received = new ReceivedRequest(
                ctx.Request.Method, ctx.Request.Path.Value!, ctx.Request.QueryString.Value ?? "",
                ctx.Request.ContentType ?? "", body);
            self().Received.Enqueue(received);
            ctx.Response.Headers.Location = "/api/invoices/" + InvoiceId;
            return Results.Json(received, statusCode: StatusCodes.Status201Created);
        });

        // A conflict exactly as the real API words it.
        app.MapPost("/api/suppliers", (HttpContext ctx) => Results.Text(
            Problem(409, "Conflict", "A supplier with code V-10442 already exists.", ctx.Request.Path),
            "application/problem+json", statusCode: StatusCodes.Status409Conflict));

        app.MapFallback((HttpContext ctx) => NotFound(ctx, $"Nothing was found at {ctx.Request.Path}."));
    }

    private static IResult NotFound(HttpContext ctx, string detail) =>
        Results.Text(Problem(404, "Not Found", detail, ctx.Request.Path), "application/problem+json",
            statusCode: StatusCodes.Status404NotFound);

    public static string Problem(int status, string title, string detail, string path) => new JsonObject
    {
        ["type"] = "https://tools.ietf.org/html/rfc9110",
        ["title"] = title,
        ["status"] = status,
        ["detail"] = detail,
        ["traceId"] = "fake-trace-" + path,
    }.ToJsonString();

    public const string Kpis = """
        { "poComplianceBySpendPct": 92.5, "poComplianceByCountPct": 90.0, "firstPassMatchRatePct": 50.0,
          "retroPoRatePct": 10.0, "openExceptions": 7, "invoiceCount": 9 }
        """;

    private static JsonNode ListItem(string id, string number, string date, string status, string? reason, decimal total) =>
        new JsonObject
        {
            ["id"] = id,
            ["invoiceNumber"] = number,
            ["supplierId"] = SupplierId.ToString(),
            ["supplierCode"] = "V-10442",
            ["supplierName"] = "Bluepeak Medical Supply",
            ["poId"] = PoId.ToString(),
            ["poNumber"] = "4500123",
            ["invoiceDate"] = date,
            ["dueDate"] = "2026-12-31",
            ["currency"] = "USD",
            ["status"] = status,
            ["blockReason"] = reason,
            ["isRetroPo"] = false,
            ["total"] = total,
        };

    /// <summary>Deliberately out of order. Seven Blocked (two share a date) and one Matched.</summary>
    private static JsonNode[] InvoiceList() =>
    [
        ListItem("a0000000-0000-0000-0000-000000000001", "INV-1001", "2026-09-01", "Blocked", "Price: oldest", 10.00m),
        ListItem("a0000000-0000-0000-0000-000000000005", "INV-1005", "2026-10-05", "Blocked", "Quantity: r5", 50.00m),
        ListItem("a0000000-0000-0000-0000-000000000009", "INV-1009", "2026-11-01", "Matched", null, 90.00m),
        ListItem("a0000000-0000-0000-0000-000000000003", "INV-1003", "2026-10-03", "Blocked", "Quantity: r3", 30.00m),
        ListItem("a0000000-0000-0000-0000-000000000007", "INV-1007", "2026-10-07", "Blocked", "Quantity: r7", 70.00m),
        ListItem("a0000000-0000-0000-0000-000000000002", "INV-1002", "2026-09-02", "Blocked", "Price: r2", 20.00m),
        ListItem("a0000000-0000-0000-0000-000000000016", "INV-1006B", "2026-10-06", "Blocked", "Quantity: r6b", 61.00m),
        ListItem("a0000000-0000-0000-0000-000000000006", "INV-1006A", "2026-10-06", "Blocked", "Quantity: r6a", 60.00m),
    ];

    public static readonly string InvoiceDetail = $$"""
        {
          "id": "{{InvoiceId}}", "invoiceNumber": "INV-77812", "normalizedNumber": "77812",
          "supplierId": "{{SupplierId}}", "supplierCode": "V-10442", "supplierName": "Bluepeak Medical Supply",
          "poId": "{{PoId}}", "poNumber": "4500123",
          "invoiceDate": "2026-10-08", "dueDate": "2026-11-07", "currency": "USD",
          "status": "Blocked", "blockReason": "Quantity: billed 2 more than received (line 1)",
          "firstMatchPassed": false, "isRetroPo": false, "createdAt": "2026-10-08T14:00:00",
          "subtotal": 1020.00, "taxAmount": 0.00, "total": 1020.00,
          "lines": [{ "lineNo": 1, "poLineNo": 1, "itemCode": "GLV-NIT-M", "uom": "BX", "description": "Nitrile gloves",
                      "quantity": 100.000, "unitPrice": 10.2000, "amount": 1020.00 }],
          "checks": [{ "seq": 1, "name": "Supplier", "lineNo": null, "poValue": "—", "receiptValue": "—",
                       "invoiceValue": "—", "outcome": "Pass", "message": "supplier matches the PO" }]
        }
        """;

    public static readonly string PurchaseOrderDetail = $$"""
        {
          "id": "{{PoId}}", "number": "4500123", "supplierId": "{{SupplierId}}", "supplierCode": "V-10442",
          "supplierName": "Bluepeak Medical Supply", "requesterName": "Dana Ruiz", "costCenterCode": "CC-200",
          "currency": "USD", "poDate": "2026-10-01", "status": "Approved", "approverRole": "Cost center owner",
          "approvedBy": "Demo User", "approvedAt": "2026-10-02T09:00:00", "total": 1000.00,
          "lines": [{ "lineNo": 1, "itemCode": "GLV-NIT-M", "description": "Nitrile gloves", "uom": "BX",
                      "quantity": 100.000, "unitPrice": 10.0000, "glAccount": "6110", "receiptRequired": true, "amount": 1000.00 }],
          "receipts": [], "invoices": []
        }
        """;

    public static readonly string Supplier = $$"""
        { "id": "{{SupplierId}}", "code": "V-10442", "legalName": "Bluepeak Medical Supply",
          "categoryCode": "MED-SUPPLIES", "categoryName": "Medical supplies", "paymentTermsDays": 30,
          "taxIdMasked": "•••• 1234", "bankMasked": "•••• 9876", "isBlocked": false }
        """;

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
