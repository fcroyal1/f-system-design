using Procurement.TestSupport;

namespace Procurement.Api.Tests;

public sealed class KpiEndpointTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Kpis_SeedData()
    {
        SkipIfNoDocker.Check();

        var kpis = await api.CreateClient().GetJsonAsync("/api/kpis");

        Assert.Equal(100.0m, kpis.GetProperty("poComplianceBySpendPct").GetDecimal());
        Assert.Equal(100.0m, kpis.GetProperty("poComplianceByCountPct").GetDecimal());
        Assert.Equal(50.0m, kpis.GetProperty("firstPassMatchRatePct").GetDecimal());
        Assert.Equal(0.0m, kpis.GetProperty("retroPoRatePct").GetDecimal());
        Assert.Equal(1, kpis.GetProperty("openExceptions").GetInt32());
        Assert.Equal(2, kpis.GetProperty("invoiceCount").GetInt32());
    }
}
