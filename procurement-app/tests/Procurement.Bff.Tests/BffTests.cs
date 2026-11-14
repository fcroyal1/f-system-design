using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Procurement.Bff.Tests;

/// <summary>A BFF whose API destination is whatever <paramref name="apiBaseUrl"/> says.</summary>
public sealed class BffFactory(string apiBaseUrl) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Api:BaseUrl", apiBaseUrl);
        // Short, so the stalled-body test finishes quickly. FakeApi answers everything else in milliseconds.
        builder.UseSetting("Api:TimeoutSeconds", "1");
    }
}

/// <summary>One FakeApi per test class, plus a BFF pointed at it.</summary>
public sealed class BffFixture : IAsyncLifetime
{
    public FakeApi Api { get; private set; } = null!;
    public BffFactory Bff { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Api = await FakeApi.StartAsync();
        Bff = new BffFactory(Api.BaseUrl);
    }

    public async ValueTask DisposeAsync()
    {
        await Bff.DisposeAsync();
        await Api.DisposeAsync();
    }
}

public sealed class BffTests(BffFixture fixture) : IClassFixture<BffFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient Client() => fixture.Bff.CreateClient();

    private static async Task<JsonNode> ReadJson(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct))!;

    [Fact]
    public async Task Api_PassThrough_ForwardsMethodPathQueryBody()
    {
        const string body = """{"invoiceNumber":"INV-77812","lines":[{"quantity":100.000,"unitPrice":10.2000}]}""";
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await Client().PostAsync("/api/invoices?x=1", content, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/invoices/" + FakeApi.InvoiceId, response.Headers.Location?.OriginalString);
        var received = Assert.Single(fixture.Api.Received);
        Assert.Equal("POST", received.Method);
        Assert.Equal("/api/invoices", received.Path);
        Assert.Equal("?x=1", received.QueryString);
        Assert.StartsWith("application/json", received.ContentType);
        Assert.Equal(body, received.Body);

        var echoed = await ReadJson(response);
        Assert.Equal(body, (string?)echoed["body"]);
    }

    [Fact]
    public async Task Api_Error_PreservesStatusAndProblemDetails()
    {
        var response = await Client().PostAsync("/api/suppliers",
            new StringContent("""{"code":"V-10442"}""", Encoding.UTF8, "application/json"), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var expected = FakeApi.Problem(409, "Conflict", "A supplier with code V-10442 already exists.", "/api/suppliers");
        Assert.Equal(expected, await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Dashboard_CombinesKpisAndExceptions()
    {
        var response = await Client().GetAsync("/bff/dashboard", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJson(response);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(FakeApi.Kpis), json["kpis"]));

        var exceptions = json["exceptions"]!.AsArray();
        // Newest invoiceDate first, ties broken by invoiceNumber; the Matched invoice never appears; max 5.
        Assert.Equal(
            ["INV-1007", "INV-1006A", "INV-1006B", "INV-1005", "INV-1003"],
            exceptions.Select(e => (string)e!["invoiceNumber"]!).ToArray());

        var first = exceptions[0]!.AsObject();
        Assert.Equal(
            ["id", "invoiceNumber", "supplierName", "total", "blockReason"],
            first.Select(p => p.Key).ToArray());
        Assert.Equal("a0000000-0000-0000-0000-000000000007", (string?)first["id"]);
        Assert.Equal("Bluepeak Medical Supply", (string?)first["supplierName"]);
        Assert.Equal(70.00m, (decimal)first["total"]!);
        Assert.Equal("Quantity: r7", (string?)first["blockReason"]);
    }

    [Fact]
    public async Task Dashboard_ApiDown_Returns502Problem()
    {
        // Port 1 on loopback refuses connections, so every call to the API fails with HttpRequestException.
        await using var downBff = new BffFactory("http://127.0.0.1:1");

        var response = await downBff.CreateClient().GetAsync("/bff/dashboard", Ct);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadJson(response);
        Assert.Equal("The procurement API is unavailable", (string?)problem["title"]);
        Assert.Equal(502, (int)problem["status"]!);
    }

    [Fact]
    public async Task InvoiceView_IncludesPoAndSupplier()
    {
        var response = await Client().GetAsync($"/bff/invoices/{FakeApi.InvoiceId}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJson(response);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(FakeApi.InvoiceDetail), json["invoice"]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(FakeApi.PurchaseOrderDetail), json["po"]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(FakeApi.Supplier), json["supplier"]));

        var missing = await Client().GetAsync($"/bff/invoices/{Guid.NewGuid()}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SpaFallback_DeepLink_ReturnsIndex()
    {
        var client = Client();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        var response = await client.GetAsync("/invoices/abc", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<title>Procurement</title>", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task UnknownApiPath_Returns404NotIndex()
    {
        var client = Client();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        var bff = await client.GetAsync("/bff/nope", Ct);
        Assert.Equal(HttpStatusCode.NotFound, bff.StatusCode);
        Assert.NotEqual("text/html", bff.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("<html", await bff.Content.ReadAsStringAsync(Ct), StringComparison.OrdinalIgnoreCase);

        var api = await client.GetAsync("/api/nope", Ct);
        Assert.Equal(HttpStatusCode.NotFound, api.StatusCode);
        Assert.Equal("application/problem+json", api.Content.Headers.ContentType?.MediaType);
        // The body is FakeApi's own 404, proving the request was forwarded rather than answered by the BFF.
        Assert.Equal(
            FakeApi.Problem(404, "Not Found", "Nothing was found at /api/nope.", "/api/nope"),
            await api.Content.ReadAsStringAsync(Ct));
    }

    private async Task Assert502Problem(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadJson(response);
        Assert.Equal("The procurement API is unavailable", (string?)problem["title"]);
        Assert.Equal(502, (int)problem["status"]!);
    }

    [Fact]
    public async Task Invoice_Api5xx_Returns502Problem()
    {
        var response = await Client().GetAsync($"/bff/invoices/{FakeApi.Invoice503Id}", Ct);

        await Assert502Problem(response);
    }

    [Fact]
    public async Task Invoice_ApiBodyStalls_Returns502Problem()
    {
        var started = System.Diagnostics.Stopwatch.StartNew();

        var response = await Client().GetAsync($"/bff/invoices/{FakeApi.InvoiceStallId}", Ct);

        await Assert502Problem(response);
        // Api:TimeoutSeconds is 1 in BffFactory; FakeApi would otherwise stall for 30 s.
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(10), $"took {started.Elapsed}");
    }

    [Fact]
    public async Task Invoice_ApiBodyCutOff_Returns502Problem()
    {
        var response = await Client().GetAsync($"/bff/invoices/{FakeApi.InvoiceCutOffId}", Ct);

        await Assert502Problem(response);
    }

    [Fact]
    public async Task Invoice_Api400_PassesThrough()
    {
        var response = await Client().GetAsync($"/bff/invoices/{FakeApi.Invoice400Id}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            FakeApi.Problem(400, "Bad Request", FakeApi.Invoice400Detail, $"/api/invoices/{FakeApi.Invoice400Id}"),
            await response.Content.ReadAsStringAsync(Ct));
    }
}
