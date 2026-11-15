using System.Text.Json;
using Procurement.TestSupport;

namespace Procurement.Api.Tests;

/// <summary>GET /api/analytics against the seed data only (this class's own database is never written to).</summary>
public sealed class AnalyticsEndpointTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Analytics_SpendByCategoryAndSupplier_FromApprovedPos()
    {
        SkipIfNoDocker.Check();
        var analytics = await api.CreateClient().GetJsonAsync("/api/analytics");

        var categories = analytics.GetProperty("spendByCategory").EnumerateArray().ToList();
        Assert.Equal(new[] { "MED-SUPPLIES", "OFFICE" }, categories.Select(c => c.GetProperty("code").GetString()));
        Assert.Equal(1000.00m, categories[0].GetProperty("amount").GetDecimal());
        Assert.Equal("Medical supplies", categories[0].GetProperty("name").GetString());
        Assert.Equal(1, categories[0].GetProperty("poCount").GetInt32());
        Assert.Equal(260.00m, categories[1].GetProperty("amount").GetDecimal());

        var suppliers = analytics.GetProperty("topSuppliers").EnumerateArray().ToList();
        Assert.Equal(new[] { "V-10442", "V-10518" }, suppliers.Select(s => s.GetProperty("code").GetString()));
        Assert.Equal("Bluepeak Medical Supply", suppliers[0].GetProperty("name").GetString());
        Assert.Equal(1000.00m, suppliers[0].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Analytics_InvoicesByStatusAndBlockReasons()
    {
        SkipIfNoDocker.Check();
        var analytics = await api.CreateClient().GetJsonAsync("/api/analytics");

        var statuses = analytics.GetProperty("invoicesByStatus").EnumerateArray()
            .ToDictionary(s => s.GetProperty("status").GetString()!, s => s);
        Assert.Equal(1, statuses["Blocked"].GetProperty("count").GetInt32());
        Assert.Equal(1020.00m, statuses["Blocked"].GetProperty("amount").GetDecimal());
        Assert.Equal(1, statuses["Matched"].GetProperty("count").GetInt32());

        var reason = Assert.Single(analytics.GetProperty("blockReasons").EnumerateArray());
        Assert.Equal("Quantity", reason.GetProperty("reason").GetString());
        Assert.Equal(1, reason.GetProperty("count").GetInt32());
        Assert.Equal(1020.00m, reason.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Analytics_MonthlyTrendAndApprovals()
    {
        SkipIfNoDocker.Check();
        var analytics = await api.CreateClient().GetJsonAsync("/api/analytics");

        var months = analytics.GetProperty("monthly").EnumerateArray()
            .ToDictionary(m => m.GetProperty("month").GetString()!, m => m);
        Assert.Equal(260.00m, months["2026-09"].GetProperty("poAmount").GetDecimal());
        Assert.Equal(1000.00m, months["2026-10"].GetProperty("poAmount").GetDecimal());
        Assert.Equal(1, months["2026-10"].GetProperty("poCount").GetInt32());
        Assert.Equal(
            months.Keys.Order(StringComparer.Ordinal),
            analytics.GetProperty("monthly").EnumerateArray().Select(m => m.GetProperty("month").GetString()));

        var approval = Assert.Single(analytics.GetProperty("approvalsByRole").EnumerateArray());
        Assert.Equal("Cost center owner", approval.GetProperty("role").GetString());
        Assert.Equal(2, approval.GetProperty("poCount").GetInt32());
        Assert.Equal(1260.00m, approval.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Analytics_PayablesAging_CoversEveryMatchedInvoiceOnce()
    {
        SkipIfNoDocker.Check();
        var analytics = await api.CreateClient().GetJsonAsync("/api/analytics");

        var buckets = analytics.GetProperty("payablesAging").EnumerateArray().ToList();
        Assert.Equal(
            new[] { "Overdue", "Due in 0–30 days", "Due in 31–60 days", "Due after 60 days" },
            buckets.Select(b => b.GetProperty("bucket").GetString()));
        Assert.Equal(1, buckets.Sum(b => b.GetProperty("count").GetInt32()));

        var matchedAmount = analytics.GetProperty("invoicesByStatus").EnumerateArray()
            .Single(s => s.GetProperty("status").GetString() == "Matched").GetProperty("amount").GetDecimal();
        Assert.Equal(matchedAmount, buckets.Sum(b => b.GetProperty("amount").GetDecimal()));
    }

    [Fact]
    public async Task Analytics_SupplierBase()
    {
        SkipIfNoDocker.Check();
        var analytics = await api.CreateClient().GetJsonAsync("/api/analytics");

        var suppliers = analytics.GetProperty("suppliers");
        Assert.Equal(30, suppliers.GetProperty("total").GetInt32());
        Assert.Equal(3, suppliers.GetProperty("blocked").GetInt32());

        var byCategory = suppliers.GetProperty("byCategory").EnumerateArray().ToList();
        Assert.Equal(11, byCategory.Count);
        Assert.Equal(30, byCategory.Sum(c => c.GetProperty("count").GetInt32()));
        Assert.Equal(JsonValueKind.True, byCategory.Single(c => c.GetProperty("code").GetString() == "UTILITIES")
            .GetProperty("isPoExempt").ValueKind);
    }
}
