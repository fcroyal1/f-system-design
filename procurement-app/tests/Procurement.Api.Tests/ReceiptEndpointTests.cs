using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Dapper;
using Procurement.TestSupport;

namespace Procurement.Api.Tests;

public sealed class ReceiptEndpointTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Guid Bluepeak = InvoiceEndpointTests.Bluepeak;
    private static readonly Guid Po4500123 = Guid.Parse("a1b2c3d4-0002-4000-8000-000004500123");

    private static object ReceiptBody(decimal quantity, int poLineNo = 1) => new
    {
        receivedBy = "Harborview Clinics – Supply room",
        packingSlip = "PS-TEST-1",
        lines = new[] { new { poLineNo, quantity } },
    };

    /// <summary>An Approved PO of 100 boxes with 98 already received; returns its id.</summary>
    private static async Task<Guid> ApprovedPoWith98ReceivedAsync(HttpClient client)
    {
        var poResponse = await client.PostJsonAsync("/api/purchase-orders", new
        {
            supplierId = Bluepeak,
            requesterName = "Harborview Clinics – Supply room",
            costCenterCode = "CC-200",
            currency = "USD",
            poDate = "2026-11-01",
            lines = new[]
            {
                new { itemCode = "GLV-NIT-M", description = "Gloves", uom = "BX", quantity = 100m, unitPrice = 10m, glAccount = "6110", receiptRequired = true },
            },
        });
        await poResponse.AssertStatusAsync(HttpStatusCode.Created);
        var poId = (await poResponse.JsonAsync()).GetProperty("id").GetGuid();
        await (await client.PostJsonAsync($"/api/purchase-orders/{poId}/approve")).AssertStatusAsync(HttpStatusCode.OK);
        await (await client.PostJsonAsync($"/api/purchase-orders/{poId}/receipts", ReceiptBody(98m))).AssertStatusAsync(HttpStatusCode.Created);
        return poId;
    }

    private async Task<decimal> TotalReceivedAsync(Guid poId)
    {
        await using var db = await api.OpenDbAsync();
        return await db.ExecuteScalarAsync<decimal>(
            """
            SELECT ISNULL(SUM(gl.quantity_received), 0)
            FROM dbo.gr_line gl JOIN dbo.goods_receipt gr ON gr.id = gl.gr_id
            WHERE gr.po_id = @poId AND gl.po_line_no = 1;
            """, new { poId });
    }

    [Fact]
    public async Task Receipt_LastTwoBoxes_AutoRematchesToMatched()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        var response = await client.PostJsonAsync($"/api/purchase-orders/{Po4500123}/receipts", ReceiptBody(2m));

        await response.AssertStatusAsync(HttpStatusCode.Created);
        var receipt = await response.JsonAsync();
        var grId = receipt.GetProperty("id").GetGuid();
        // Other tests in this class also draw from gr_number_seq, which starts after the seeded 5000987.
        Assert.True(int.Parse(receipt.GetProperty("grNumber").GetString()!) > 5000987);
        var rematched = Assert.Single(receipt.GetProperty("rematchedInvoices").EnumerateArray());
        Assert.Equal(InvoiceEndpointTests.Inv77812, rematched.GetProperty("id").GetGuid());
        Assert.Equal("Matched", rematched.GetProperty("status").GetString());

        var invoice = await client.GetJsonAsync($"/api/invoices/{InvoiceEndpointTests.Inv77812}");
        Assert.Equal("Matched", invoice.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, invoice.GetProperty("blockReason").ValueKind);
        Assert.False(invoice.GetProperty("firstMatchPassed").GetBoolean());

        await using var db = await api.OpenDbAsync();
        Assert.False(await db.ExecuteScalarAsync<bool>(
            "SELECT first_match_passed FROM dbo.invoice WHERE id = @id;", new { id = InvoiceEndpointTests.Inv77812 }));
        var audit = (await db.QueryAsync<(string Action, string EntityType, Guid EntityId, string? From, string? To)>(
            """
            SELECT action, entity_type, entity_id, from_status, to_status FROM dbo.audit_event
            WHERE entity_id IN (@grId, @invoiceId) ORDER BY id;
            """, new { grId, invoiceId = InvoiceEndpointTests.Inv77812 })).ToList();
        Assert.Equal(
            [
                ("GoodsReceived", "GoodsReceipt", grId, null, null),
                ("InvoiceMatched", "Invoice", InvoiceEndpointTests.Inv77812, "Blocked", "Matched"),
            ],
            audit);
    }

    [Fact]
    public async Task Receipt_CumulativeOverReceipt_Returns422()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();
        var poId = await ApprovedPoWith98ReceivedAsync(client);

        var response = await client.PostJsonAsync($"/api/purchase-orders/{poId}/receipts", ReceiptBody(3m));

        Assert.Equal("Line 1: receiving 3 would bring the total to 101, more than the 100 ordered",
            await response.ProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(98m, await TotalReceivedAsync(poId));
    }

    [Fact]
    public async Task Receipt_ConcurrentOverReceipt_OnlyOneSucceeds()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();
        var poId = await ApprovedPoWith98ReceivedAsync(client);

        // Hold the PO row's update lock so both requests are provably inside their transactions,
        // waiting on the same lock, before either can sum the received quantities.
        await using var blocker = await api.OpenDbAsync();
        await using var hold = blocker.BeginTransaction();
        await blocker.ExecuteAsync(
            "SELECT id FROM dbo.purchase_order WITH (UPDLOCK, HOLDLOCK) WHERE id = @poId;", new { poId }, hold);

        var first = client.PostJsonAsync($"/api/purchase-orders/{poId}/receipts", ReceiptBody(2m));
        var second = api.CreateClient().PostJsonAsync($"/api/purchase-orders/{poId}/receipts", ReceiptBody(2m));

        await using (var monitor = await api.OpenDbAsync())
        {
            var waited = Stopwatch.StartNew();
            while (await monitor.ExecuteScalarAsync<int>(
                       """
                       SELECT COUNT(*) FROM sys.dm_exec_requests
                       WHERE database_id = DB_ID() AND session_id <> @@SPID AND wait_type LIKE 'LCK[_]M[_]%';
                       """) < 2)
            {
                Assert.True(waited.Elapsed < TimeSpan.FromSeconds(30), "Both receipt requests should wait on the PO lock.");
                await Task.Delay(50, Http.Ct);
            }
        }

        hold.Rollback();
        var responses = await Task.WhenAll(first, second);

        var statuses = responses.Select(r => r.StatusCode).Order().ToList();
        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity], statuses);
        var rejected = responses.Single(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);
        Assert.Equal("Line 1: receiving 2 would bring the total to 102, more than the 100 ordered",
            await rejected.ProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(100m, await TotalReceivedAsync(poId));
    }
}
