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

        Assert.Equal(11, reference.GetProperty("categories").GetArrayLength());
        Assert.Contains(reference.GetProperty("categories").EnumerateArray(),
            c => c.GetProperty("code").GetString() == "UTILITIES" && c.GetProperty("isPoExempt").GetBoolean());
        Assert.Contains(reference.GetProperty("glAccounts").EnumerateArray(),
            g => g.GetProperty("number").GetString() == "6110");

        Assert.Equal(
            new[] { "CC-100", "CC-200", "CC-300", "CC-400" },
            reference.GetProperty("costCenters").EnumerateArray().Select(c => c.GetProperty("code").GetString()).Order());

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

    [Fact]
    public async Task Supplier_Update_ChangesFieldsAndAuditsWhatChanged()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();
        var id = await CreateSupplierAsync(client, "V-20101");

        var response = await client.PutJsonAsync($"/api/suppliers/{id}", new
        {
            legalName = "Edited Supplies Inc",
            categoryCode = "OFFICE",
            paymentTermsDays = 45,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.JsonAsync();
        Assert.Equal("V-20101", updated.GetProperty("code").GetString());
        Assert.Equal("Edited Supplies Inc", updated.GetProperty("legalName").GetString());
        Assert.Equal("OFFICE", updated.GetProperty("categoryCode").GetString());
        Assert.Equal(45, updated.GetProperty("paymentTermsDays").GetInt32());
        // Tax ID and bank account were not sent, so they are kept.
        Assert.Equal(Bullets + "1111", updated.GetProperty("taxIdMasked").GetString());
        Assert.Equal(Bullets + "2222", updated.GetProperty("bankMasked").GetString());

        var history = await client.GetJsonAsync($"/api/suppliers/{id}/history");
        var change = history.EnumerateArray().First();
        Assert.Equal("SupplierUpdated", change.GetProperty("action").GetString());
        var detail = change.GetProperty("detail").GetString();
        Assert.Contains("legal name", detail);
        Assert.Contains("category", detail);
        Assert.Contains("payment terms 30 → 45 days", detail);
        Assert.DoesNotContain("bank", detail);
    }

    [Fact]
    public async Task Supplier_Update_BankChange_StoresLast4AndIsFlaggedInHistory()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();
        var id = await CreateSupplierAsync(client, "V-20102");

        var response = await client.PutJsonAsync($"/api/suppliers/{id}", new
        {
            legalName = "Bank Change Ltd",
            categoryCode = "MED-SUPPLIES",
            paymentTermsDays = 30,
            bankAccount = "555500009876",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync(Http.Ct);
        Assert.DoesNotContain("555500009876", raw);
        Assert.Equal(Bullets + "9876", JsonDocument.Parse(raw).RootElement.GetProperty("bankMasked").GetString());

        var history = await client.GetJsonAsync($"/api/suppliers/{id}/history");
        var change = history.EnumerateArray().First();
        Assert.Equal("SupplierBankChanged", change.GetProperty("action").GetString());
        Assert.Contains("•••• 2222 → •••• 9876", change.GetProperty("detail").GetString());

        await using var db = await api.OpenDbAsync();
        Assert.Equal("9876", await db.ExecuteScalarAsync<string>(
            "SELECT bank_last4 FROM dbo.supplier WHERE id = @id;", new { id }));
    }

    [Fact]
    public async Task Supplier_Update_Block_RecordsStatusChange()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();
        var id = await CreateSupplierAsync(client, "V-20103");

        var response = await client.PutJsonAsync($"/api/suppliers/{id}", new
        {
            legalName = "Block Me Co",
            categoryCode = "MED-SUPPLIES",
            paymentTermsDays = 30,
            isBlocked = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.JsonAsync()).GetProperty("isBlocked").GetBoolean());

        var history = await client.GetJsonAsync($"/api/suppliers/{id}/history");
        var blocked = Assert.Single(history.EnumerateArray(), e => e.GetProperty("action").GetString() == "SupplierBlocked");
        Assert.Equal("Active", blocked.GetProperty("fromStatus").GetString());
        Assert.Equal("Blocked", blocked.GetProperty("toStatus").GetString());
    }

    [Fact]
    public async Task Supplier_Update_NoChanges_WritesNoHistory()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();
        var id = await CreateSupplierAsync(client, "V-20104");

        var response = await client.PutJsonAsync($"/api/suppliers/{id}", new
        {
            legalName = "Supplier V-20104",
            categoryCode = "MED-SUPPLIES",
            paymentTermsDays = 30,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await client.GetJsonAsync($"/api/suppliers/{id}/history")).GetArrayLength());
    }

    [Fact]
    public async Task Supplier_Update_UnknownId_Returns404()
    {
        SkipIfNoDocker.Check();

        var response = await api.CreateClient().PutJsonAsync($"/api/suppliers/{Guid.NewGuid()}", new
        {
            legalName = "Nobody",
            categoryCode = "OFFICE",
            paymentTermsDays = 30,
        });

        await response.ProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Supplier_Update_UnknownCategory_Returns400()
    {
        SkipIfNoDocker.Check();
        var client = api.CreateClient();
        var id = await CreateSupplierAsync(client, "V-20105");

        var response = await client.PutJsonAsync($"/api/suppliers/{id}", new
        {
            legalName = "Bad Category",
            categoryCode = "NOPE",
            paymentTermsDays = 30,
        });

        Assert.Contains("NOPE", await response.ProblemAsync(HttpStatusCode.BadRequest));
    }

    private static async Task<Guid> CreateSupplierAsync(HttpClient client, string code)
    {
        var response = await client.PostJsonAsync("/api/suppliers", new
        {
            code,
            legalName = $"Supplier {code}",
            categoryCode = "MED-SUPPLIES",
            paymentTermsDays = 30,
            taxId = "11-0001111",
            bankAccount = "000022222",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.JsonAsync()).GetProperty("id").GetGuid();
    }
}
