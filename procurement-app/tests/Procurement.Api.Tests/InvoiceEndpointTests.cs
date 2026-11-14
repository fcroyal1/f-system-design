using System.Net;
using System.Text.Json;
using Dapper;
using Procurement.TestSupport;

namespace Procurement.Api.Tests;

public sealed class InvoiceEndpointTests(SqlServerFixture sql, ApiFactory api) : IClassFixture<ApiFactory>
{
    internal static readonly Guid Bluepeak = Guid.Parse("a1b2c3d4-0001-4000-8000-000000010442");
    internal static readonly Guid Northline = Guid.Parse("a1b2c3d4-0001-4000-8000-000000010518");
    internal static readonly Guid Inv77812 = Guid.Parse("a1b2c3d4-0004-4000-8000-000000077812");

    internal static object InvoiceBody(
        Guid supplierId, string invoiceNumber, string poNumber, decimal quantity, decimal unitPrice,
        string invoiceDate = "2026-11-10", decimal taxAmount = 0m, string itemCode = "GLV-NIT-M", string uom = "BX") => new
    {
        supplierId,
        invoiceNumber,
        invoiceDate,
        currency = "USD",
        poNumber,
        taxAmount,
        lines = new[]
        {
            new { poLineNo = 1, itemCode, uom, description = "Line item", quantity, unitPrice },
        },
    };

    private static async Task<JsonElement> CreateInvoiceAsync(HttpClient client, object body)
    {
        var response = await client.PostJsonAsync("/api/invoices", body);
        await response.AssertStatusAsync(HttpStatusCode.Created);
        var invoice = await response.JsonAsync();
        Assert.Equal($"/api/invoices/{invoice.GetProperty("id").GetGuid()}", response.Headers.Location?.OriginalString);
        return invoice;
    }

    private sealed record CheckRow(int Seq, string Name, int? LineNo, string PoValue, string ReceiptValue, string InvoiceValue, string Outcome, string Message);

    private sealed record InvoiceState(string Status, string? BlockReason, bool? FirstMatchPassed);

    private static async Task<(InvoiceState State, List<CheckRow> Checks)> ReadStateAsync(ApiFactory factory, Guid id)
    {
        await using var db = await factory.OpenDbAsync();
        var state = await db.QuerySingleAsync<InvoiceState>(
            "SELECT status AS Status, block_reason AS BlockReason, first_match_passed AS FirstMatchPassed FROM dbo.invoice WHERE id = @id;",
            new { id });
        var checks = (await db.QueryAsync<CheckRow>(
            """
            SELECT seq AS Seq, name AS Name, line_no AS [LineNo], po_value AS PoValue, receipt_value AS ReceiptValue,
                   invoice_value AS InvoiceValue, outcome AS Outcome, message AS Message
            FROM dbo.match_check WHERE invoice_id = @id ORDER BY seq;
            """, new { id })).ToList();
        return (state, checks);
    }

    [Fact]
    public async Task Rematch_SeededGuideInvoice_ReproducesSeededChecks()
    {
        SkipIfNoDocker.Check();
        // Its own database: other tests in this class add invoices against PO 4500123.
        await using var fresh = new ApiFactory(sql);
        await fresh.InitializeAsync();
        var client = fresh.CreateClient();

        var (seededState, seededChecks) = await ReadStateAsync(fresh, Inv77812);
        Assert.Equal(7, seededChecks.Count);

        var response = await client.PostJsonAsync($"/api/invoices/{Inv77812}/rematch");
        await response.AssertStatusAsync(HttpStatusCode.OK);

        var (state, checks) = await ReadStateAsync(fresh, Inv77812);
        Assert.Equal(seededState, state);
        Assert.Equal(new InvoiceState("Blocked", "Quantity: billed 2 more than received (line 1)", false), state);
        Assert.Equal(7, checks.Count);
        for (var i = 0; i < 7; i++)
        {
            Assert.Equal(seededChecks[i], checks[i]);
        }

        var body = await response.JsonAsync();
        Assert.Equal("Blocked", body.GetProperty("status").GetString());
        Assert.Equal(7, body.GetProperty("checks").GetArrayLength());
    }

    [Fact]
    public async Task Invoice_DuplicateNumberVariant_Returns409()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        var response = await client.PostJsonAsync("/api/invoices", InvoiceBody(Bluepeak, "inv77812", "4500123", 100m, 10.20m));

        Assert.Equal("Invoice INV-77812 from Bluepeak Medical Supply is already recorded",
            await response.ProblemAsync(HttpStatusCode.Conflict));
        await using var db = await api.OpenDbAsync();
        Assert.Equal(0, await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.invoice WHERE invoice_number = 'inv77812';"));
    }

    [Fact]
    public async Task Invoice_SameNumberOtherSupplier_Accepted()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        var invoice = await CreateInvoiceAsync(client,
            InvoiceBody(Northline, "INV-77812", "4500118", 5m, 6.50m, itemCode: "PPR-A4-500", uom: "RM"));

        Assert.Equal("INV-77812", invoice.GetProperty("invoiceNumber").GetString());
        Assert.Equal(Northline, invoice.GetProperty("supplierId").GetGuid());
        Assert.Equal("4500118", invoice.GetProperty("poNumber").GetString());
    }

    [Fact]
    public async Task Invoice_NumberNormalizesToEmpty_Returns400()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        var response = await client.PostJsonAsync("/api/invoices", InvoiceBody(Bluepeak, "INV-", "4500123", 1m, 10m));

        Assert.Equal("Invoice number must contain digits or letters after the prefix",
            await response.ProblemAsync(HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Invoice_UnknownPo_SavedBlockedWithReason()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        var invoice = await CreateInvoiceAsync(client, InvoiceBody(Bluepeak, "INV-80001", "4599999", 1m, 10m));
        var id = invoice.GetProperty("id").GetGuid();

        Assert.Equal("Blocked", invoice.GetProperty("status").GetString());
        Assert.Equal("PO: PO 4599999 not found", invoice.GetProperty("blockReason").GetString());
        Assert.Equal(JsonValueKind.Null, invoice.GetProperty("poId").ValueKind);
        Assert.Empty(invoice.GetProperty("checks").EnumerateArray());

        var (state, checks) = await ReadStateAsync(api, id);
        Assert.Equal(new InvoiceState("Blocked", "PO: PO 4599999 not found", null), state);
        Assert.Empty(checks);

        await using var db = await api.OpenDbAsync();
        var audit = Assert.Single(await db.QueryAsync<(string Action, string EntityType, string? From, string To)>(
            "SELECT action, entity_type, from_status, to_status FROM dbo.audit_event WHERE entity_id = @id;", new { id }));
        Assert.Equal(("InvoiceBlocked", "Invoice", (string?)null, "Blocked"), audit);
    }

    [Fact]
    public async Task Invoice_DraftPo_Blocked()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        var poResponse = await client.PostJsonAsync("/api/purchase-orders", new
        {
            supplierId = Bluepeak,
            requesterName = "Harborview Clinics – Supply room",
            costCenterCode = "CC-200",
            currency = "USD",
            poDate = "2026-11-01",
            lines = new[]
            {
                new { itemCode = "GLV-NIT-M", description = "Gloves", uom = "BX", quantity = 10m, unitPrice = 10m, glAccount = "6110", receiptRequired = false },
            },
        });
        await poResponse.AssertStatusAsync(HttpStatusCode.Created);
        var poNumber = (await poResponse.JsonAsync()).GetProperty("number").GetString()!;

        var invoice = await CreateInvoiceAsync(client, InvoiceBody(Bluepeak, "INV-80002", poNumber, 10m, 10m));

        Assert.Equal("Blocked", invoice.GetProperty("status").GetString());
        Assert.Equal("PO status: PO is Draft, not approved", invoice.GetProperty("blockReason").GetString());
        Assert.False(invoice.GetProperty("firstMatchPassed").GetBoolean());
    }

    [Fact]
    public async Task Invoice_BlockedSupplier_Blocked()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        var supplierResponse = await client.PostJsonAsync("/api/suppliers", new
        {
            code = "V-30001", legalName = "Shadow Supplies", categoryCode = "MED-SUPPLIES", paymentTermsDays = 30,
        });
        await supplierResponse.AssertStatusAsync(HttpStatusCode.Created);
        var supplierId = (await supplierResponse.JsonAsync()).GetProperty("id").GetGuid();
        await using (var db = await api.OpenDbAsync())
        {
            await db.ExecuteAsync("UPDATE dbo.supplier SET is_blocked = 1 WHERE id = @supplierId;", new { supplierId });
        }

        var invoice = await CreateInvoiceAsync(client, InvoiceBody(supplierId, "SH-1", "4500123", 1m, 10m));
        var id = invoice.GetProperty("id").GetGuid();

        Assert.Equal("Blocked", invoice.GetProperty("status").GetString());
        Assert.Equal("Supplier: supplier V-30001 is blocked", invoice.GetProperty("blockReason").GetString());
        Assert.Empty(invoice.GetProperty("checks").EnumerateArray());
        var (state, checks) = await ReadStateAsync(api, id);
        Assert.Null(state.FirstMatchPassed);
        Assert.Empty(checks);
    }

    [Fact]
    public async Task Invoice_BeforePoDate_FlaggedRetro()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        // PO 4500118 is dated 2026-09-20.
        var invoice = await CreateInvoiceAsync(client,
            InvoiceBody(Northline, "NL-1900", "4500118", 1m, 6.50m, invoiceDate: "2026-09-15", itemCode: "PPR-A4-500", uom: "RM"));

        Assert.True(invoice.GetProperty("isRetroPo").GetBoolean());
        Assert.Equal("2026-09-15", invoice.GetProperty("invoiceDate").GetString());
        Assert.Equal("2026-10-30", invoice.GetProperty("dueDate").GetString()); // Northline terms: 45 days
    }

    [Fact]
    public async Task Invoice_SecondInvoice_CountsMatchedOnly()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        var seeded = await client.GetJsonAsync($"/api/invoices/{Inv77812}");
        Assert.Equal("Blocked", seeded.GetProperty("status").GetString());

        var invoice = await CreateInvoiceAsync(client, InvoiceBody(Bluepeak, "INV-77900", "4500123", 98m, 10.00m));

        Assert.Equal("Matched", invoice.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, invoice.GetProperty("blockReason").ValueKind);
        Assert.True(invoice.GetProperty("firstMatchPassed").GetBoolean());
        Assert.All(invoice.GetProperty("checks").EnumerateArray(), c => Assert.Equal("Pass", c.GetProperty("outcome").GetString()));

        var blocked = await client.GetJsonAsync("/api/invoices?status=Blocked");
        Assert.Contains(blocked.EnumerateArray(), i => i.GetProperty("id").GetGuid() == Inv77812);
        Assert.DoesNotContain(blocked.EnumerateArray(), i => i.GetProperty("id").GetGuid() == invoice.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Invoice_Detail_TotalIncludesTax()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        var created = await CreateInvoiceAsync(client, InvoiceBody(Bluepeak, "INV-90001", "4500123", 100m, 10.20m, taxAmount: 12.50m));
        var invoice = await client.GetJsonAsync($"/api/invoices/{created.GetProperty("id").GetGuid()}");

        Assert.Equal(1020.00m, invoice.GetProperty("subtotal").GetDecimal());
        Assert.Equal(12.50m, invoice.GetProperty("taxAmount").GetDecimal());
        Assert.Equal(1032.50m, invoice.GetProperty("total").GetDecimal());
        var line = Assert.Single(invoice.GetProperty("lines").EnumerateArray());
        Assert.Equal(1020.00m, line.GetProperty("amount").GetDecimal());
        Assert.Equal("Bluepeak Medical Supply", invoice.GetProperty("supplierName").GetString());
        Assert.Equal("2026-12-10", invoice.GetProperty("dueDate").GetString()); // Bluepeak terms: 30 days
    }

    [Fact]
    public async Task Invoice_TwoLinesSamePoLine_OverReceived_Blocked()
    {
        SkipIfNoDocker.Check();
        // Its own database: other tests in this class add Matched invoices against PO 4500123.
        await using var fresh = new ApiFactory(sql);
        await fresh.InitializeAsync();
        var client = fresh.CreateClient();

        // PO 4500123 line 1: 100 @ 10.00 ordered, 98 received, nothing Matched yet.
        var invoice = await CreateInvoiceAsync(client, new
        {
            supplierId = Bluepeak,
            invoiceNumber = "INV-91001",
            invoiceDate = "2026-11-10",
            currency = "USD",
            poNumber = "4500123",
            taxAmount = 0m,
            lines = new[]
            {
                new { poLineNo = 1, itemCode = "GLV-NIT-M", uom = "BX", description = "First half", quantity = 98m, unitPrice = 10.00m },
                new { poLineNo = 1, itemCode = "GLV-NIT-M", uom = "BX", description = "Billed again", quantity = 98m, unitPrice = 10.00m },
            },
        });

        Assert.Equal("Blocked", invoice.GetProperty("status").GetString());
        Assert.Equal("Quantity: billed 98 more than received (line 2)", invoice.GetProperty("blockReason").GetString());
    }
}
