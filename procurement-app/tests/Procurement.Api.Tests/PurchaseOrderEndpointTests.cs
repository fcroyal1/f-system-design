using System.Net;
using System.Text.Json;
using Dapper;
using Procurement.TestSupport;

namespace Procurement.Api.Tests;

public sealed class PurchaseOrderEndpointTests(SqlServerFixture sql, ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Guid Bluepeak = Guid.Parse("a1b2c3d4-0001-4000-8000-000000010442");

    private static async Task<JsonElement> CreatePoAsync(HttpClient client, decimal quantity, decimal unitPrice)
    {
        var response = await client.PostJsonAsync("/api/purchase-orders", new
        {
            supplierId = Bluepeak,
            requesterName = "Harborview Clinics – Supply room",
            costCenterCode = "CC-200",
            currency = "USD",
            poDate = "2026-11-13",
            lines = new[]
            {
                new
                {
                    itemCode = "GLV-NIT-M",
                    description = "Nitrile exam gloves, medium, box of 100",
                    uom = "BX",
                    quantity,
                    unitPrice,
                    glAccount = "6110",
                    receiptRequired = true,
                },
            },
        });

        await response.AssertStatusAsync(HttpStatusCode.Created);
        var po = await response.JsonAsync();
        Assert.Equal($"/api/purchase-orders/{po.GetProperty("id").GetGuid()}", response.Headers.Location?.OriginalString);
        return po;
    }

    [Fact]
    public async Task Po_Create_IsDraftWithNextNumber()
    {
        SkipIfNoDocker.Check();
        // Its own database: the other tests in this class also draw PO numbers from the sequence.
        await using var fresh = new ApiFactory(sql);
        await fresh.InitializeAsync();
        var client = fresh.CreateClient();

        var po = await CreatePoAsync(client, 100m, 10m);

        Assert.Equal("4500124", po.GetProperty("number").GetString());
        Assert.Equal("Draft", po.GetProperty("status").GetString());
        Assert.Equal(1000.00m, po.GetProperty("total").GetDecimal());
        Assert.Equal("2026-11-13", po.GetProperty("poDate").GetString());
        Assert.Equal(JsonValueKind.Null, po.GetProperty("approverRole").ValueKind);
        var line = Assert.Single(po.GetProperty("lines").EnumerateArray());
        Assert.Equal(1, line.GetProperty("lineNo").GetInt32());
        Assert.Equal(100m, line.GetProperty("quantity").GetDecimal());

        var fetched = await client.GetJsonAsync($"/api/purchase-orders/{po.GetProperty("id").GetGuid()}");
        Assert.Equal("4500124", fetched.GetProperty("number").GetString());
        Assert.Contains((await client.GetJsonAsync("/api/purchase-orders")).EnumerateArray(),
            p => p.GetProperty("number").GetString() == "4500124" && p.GetProperty("total").GetDecimal() == 1000.00m);
    }

    [Fact]
    public async Task Po_Approve_SetsTierByTotal()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        var small = await CreatePoAsync(client, 100m, 10m);
        var smallId = small.GetProperty("id").GetGuid();
        var approveSmall = await client.PostJsonAsync($"/api/purchase-orders/{smallId}/approve");
        Assert.Equal(HttpStatusCode.OK, approveSmall.StatusCode);
        var approved = await approveSmall.JsonAsync();
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        Assert.Equal("Cost center owner", approved.GetProperty("approverRole").GetString());
        Assert.Equal("Demo User", approved.GetProperty("approvedBy").GetString());

        var large = await CreatePoAsync(client, 3000m, 10m);
        Assert.Equal(30000.00m, large.GetProperty("total").GetDecimal());
        var approveLarge = await client.PostJsonAsync($"/api/purchase-orders/{large.GetProperty("id").GetGuid()}/approve");
        Assert.Equal(HttpStatusCode.OK, approveLarge.StatusCode);
        Assert.Equal("VP Finance", (await approveLarge.JsonAsync()).GetProperty("approverRole").GetString());

        await using var db = await api.OpenDbAsync();
        var audit = Assert.Single(await db.QueryAsync<(string Actor, string Action, string EntityType, string From, string To)>(
            """
            SELECT actor, action, entity_type, from_status, to_status
            FROM dbo.audit_event WHERE entity_id = @smallId;
            """, new { smallId }));
        Assert.Equal(("Demo User", "PoApproved", "PurchaseOrder", "Draft", "Approved"), audit);
    }

    [Fact]
    public async Task Po_ApproveTwice_Returns422()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        var po = await CreatePoAsync(client, 5m, 20m);
        var id = po.GetProperty("id").GetGuid();
        var number = po.GetProperty("number").GetString();

        Assert.Equal(HttpStatusCode.OK, (await client.PostJsonAsync($"/api/purchase-orders/{id}/approve")).StatusCode);
        var second = await client.PostJsonAsync($"/api/purchase-orders/{id}/approve");

        var detail = await second.ProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal($"PO {number} is Approved; only Draft POs can be approved", detail);

        await using var db = await api.OpenDbAsync();
        Assert.Equal(1, await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.audit_event WHERE entity_id = @id;", new { id }));
    }

    [Fact]
    public async Task Po_Detail_GuideExample_HasReceiptAndInvoice()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        var summary = Assert.Single((await client.GetJsonAsync("/api/purchase-orders")).EnumerateArray(),
            p => p.GetProperty("number").GetString() == "4500123");
        Assert.Equal("V-10442", summary.GetProperty("supplierCode").GetString());

        var po = await client.GetJsonAsync($"/api/purchase-orders/{summary.GetProperty("id").GetGuid()}");
        Assert.Equal("Approved", po.GetProperty("status").GetString());
        Assert.Equal("Cost center owner", po.GetProperty("approverRole").GetString());
        Assert.Equal(1000.00m, po.GetProperty("total").GetDecimal());
        Assert.Equal("2026-10-01", po.GetProperty("poDate").GetString());

        var line = Assert.Single(po.GetProperty("lines").EnumerateArray());
        Assert.Equal("GLV-NIT-M", line.GetProperty("itemCode").GetString());
        Assert.Equal(10m, line.GetProperty("unitPrice").GetDecimal());

        var receipt = Assert.Single(po.GetProperty("receipts").EnumerateArray());
        Assert.Equal("5000987", receipt.GetProperty("grNumber").GetString());
        Assert.Equal(98m, receipt.GetProperty("quantityReceived").GetDecimal());

        var invoice = Assert.Single(po.GetProperty("invoices").EnumerateArray());
        Assert.Equal("INV-77812", invoice.GetProperty("invoiceNumber").GetString());
        Assert.Equal("Blocked", invoice.GetProperty("status").GetString());
        Assert.Equal("Quantity: billed 2 more than received (line 1)", invoice.GetProperty("blockReason").GetString());
        Assert.Equal(1020.00m, invoice.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Po_UnknownId_Returns404()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();
        var id = Guid.NewGuid();

        var detail = await (await client.GetAsync($"/api/purchase-orders/{id}", Http.Ct)).ProblemAsync(HttpStatusCode.NotFound);
        Assert.Contains(id.ToString(), detail);

        await (await client.PostJsonAsync($"/api/purchase-orders/{id}/approve")).ProblemAsync(HttpStatusCode.NotFound);
    }
}
