using System.Net;
using System.Text.Json;
using Dapper;
using Procurement.TestSupport;

namespace Procurement.Api.Tests;

public sealed class SupplierEndpointTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private const string Bullets = "•••• ";

    [Fact]
    public async Task Reference_ReturnsSeededLists()
    {
        SkipIfNoDocker.Check();
        var reference = await api.CreateClient().GetJsonAsync("/api/reference");

        Assert.Equal(8, reference.GetProperty("categories").GetArrayLength());
        Assert.Contains(reference.GetProperty("categories").EnumerateArray(),
            c => c.GetProperty("code").GetString() == "UTILITIES" && c.GetProperty("isPoExempt").GetBoolean());
        Assert.Contains(reference.GetProperty("glAccounts").EnumerateArray(),
            g => g.GetProperty("number").GetString() == "6110");

        var costCenter = Assert.Single(reference.GetProperty("costCenters").EnumerateArray());
        Assert.Equal("CC-200", costCenter.GetProperty("code").GetString());

        var tiers = reference.GetProperty("doaTiers").EnumerateArray().ToList();
        Assert.Equal(
            new[] { "Cost center owner", "Director", "VP Finance", "CFO" },
            tiers.Select(t => t.GetProperty("approverRole").GetString()));
        Assert.Equal(5000.00m, tiers[0].GetProperty("maxAmount").GetDecimal());
    }

    [Fact]
    public async Task Suppliers_List_IncludesSeededMasked()
    {
        SkipIfNoDocker.Check();
        var suppliers = await api.CreateClient().GetJsonAsync("/api/suppliers");

        var bluepeak = Assert.Single(suppliers.EnumerateArray(), s => s.GetProperty("code").GetString() == "V-10442");
        Assert.Equal("Bluepeak Medical Supply", bluepeak.GetProperty("legalName").GetString());
        Assert.Equal(Bullets + "4421", bluepeak.GetProperty("taxIdMasked").GetString());
        Assert.Equal(Bullets + "6630", bluepeak.GetProperty("bankMasked").GetString());

        var northline = Assert.Single(suppliers.EnumerateArray(), s => s.GetProperty("code").GetString() == "V-10518");
        Assert.Equal(JsonValueKind.Null, northline.GetProperty("taxIdMasked").ValueKind);
        Assert.Equal(JsonValueKind.Null, northline.GetProperty("bankMasked").ValueKind);

        // Only the masked forms leave the API: no raw tax / bank fields, no bare last-4 values.
        foreach (var supplier in suppliers.EnumerateArray())
        {
            foreach (var property in supplier.EnumerateObject())
            {
                Assert.DoesNotContain(property.Name, new[] { "taxId", "bankAccount", "taxIdLast4", "bankLast4" });
                if (property.Value.ValueKind == JsonValueKind.String && property.Name.EndsWith("Masked", StringComparison.Ordinal))
                {
                    Assert.StartsWith(Bullets, property.Value.GetString());
                }
            }
        }

        var detail = await api.CreateClient().GetJsonAsync($"/api/suppliers/{bluepeak.GetProperty("id").GetGuid()}");
        Assert.Equal(Bullets + "4421", detail.GetProperty("taxIdMasked").GetString());
    }

    [Fact]
    public async Task Supplier_Create_StoresLast4Only()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();

        var response = await client.PostJsonAsync("/api/suppliers", new
        {
            code = "V-20001",
            legalName = "Cedar Lab Services",
            categoryCode = "MED-SUPPLIES",
            paymentTermsDays = 30,
            taxId = "98-7654321",
            bankAccount = "000123456789",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync(Http.Ct);
        Assert.DoesNotContain("7654321", raw);
        Assert.DoesNotContain("123456789", raw);

        var created = JsonDocument.Parse(raw).RootElement;
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal($"/api/suppliers/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal("V-20001", created.GetProperty("code").GetString());
        Assert.Equal(Bullets + "4321", created.GetProperty("taxIdMasked").GetString());
        Assert.Equal(Bullets + "6789", created.GetProperty("bankMasked").GetString());

        var fetched = await client.GetJsonAsync($"/api/suppliers/{id}");
        Assert.Equal(Bullets + "4321", fetched.GetProperty("taxIdMasked").GetString());

        await using var db = await api.OpenDbAsync();
        var stored = await db.QuerySingleAsync<(string TaxIdLast4, string BankLast4)>(
            "SELECT tax_id_last4, bank_last4 FROM dbo.supplier WHERE id = @id;", new { id });
        Assert.Equal(("4321", "6789"), stored);
    }

    [Fact]
    public async Task Supplier_DuplicateCode_Returns409()
    {
        SkipIfNoDocker.Check();

        var response = await api.CreateClient().PostJsonAsync("/api/suppliers", new
        {
            code = "V-10442",
            legalName = "Bluepeak Medical Supply (again)",
            categoryCode = "MED-SUPPLIES",
            paymentTermsDays = 30,
        });

        var detail = await response.ProblemAsync(HttpStatusCode.Conflict);
        Assert.Contains("V-10442", detail);
    }

    [Fact]
    public async Task Supplier_MissingName_Returns400Problem()
    {
        SkipIfNoDocker.Check();

        var response = await api.CreateClient().PostJsonAsync("/api/suppliers", new
        {
            code = "V-20002",
            categoryCode = "OFFICE",
            paymentTermsDays = 30,
        });

        var detail = await response.ProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("legalName", detail);
    }

    [Fact]
    public async Task Supplier_Create_NegativeTerms_Returns400()
    {
        SkipIfNoDocker.Check();

        var response = await api.CreateClient().PostJsonAsync("/api/suppliers", new
        {
            code = "V-20003",
            legalName = "Negative Terms Ltd",
            categoryCode = "OFFICE",
            paymentTermsDays = -1,
        });

        Assert.Equal("paymentTermsDays must be 0 or more.", await response.ProblemAsync(HttpStatusCode.BadRequest));
    }
}
