using Procurement.Domain.Policies;
using Xunit;

namespace Procurement.Domain.Tests;

public class PolicyTests
{
    private static readonly IReadOnlyList<DoaTier> Tiers =
    [
        new(5000m, "Cost center owner"),
        new(25000m, "Director"),
        new(100000m, "VP Finance"),
        new(decimal.MaxValue, "CFO"),
    ];

    [Theory]
    [InlineData("INV-001", "1")]
    [InlineData("inv001", "1")]
    [InlineData("1", "1")]
    [InlineData("INV-77812", "77812")]
    [InlineData("A12B", "12B")]
    [InlineData("000", "0")]
    public void Normalize_Variants_SameKey(string raw, string expected) =>
        Assert.Equal(expected, InvoiceNumberNormalizer.Normalize(raw));

    [Theory]
    [InlineData("INV-")]
    [InlineData("---")]
    public void Normalize_OnlyPrefix_ReturnsEmpty(string raw) =>
        Assert.Equal("", InvoiceNumberNormalizer.Normalize(raw));

    [Theory]
    [InlineData("1000", "Cost center owner")]
    [InlineData("5000", "Cost center owner")]
    [InlineData("5000.01", "Director")]
    [InlineData("25000", "Director")]
    [InlineData("30000", "VP Finance")]
    [InlineData("100000.01", "CFO")]
    public void Route_ByTotal(string total, string role) =>
        Assert.Equal(role, ApprovalRouter.Route(decimal.Parse(total, System.Globalization.CultureInfo.InvariantCulture), Tiers));

    [Fact]
    public void IsRetro_InvoiceBeforePo_True()
    {
        Assert.True(RetroPoRule.IsRetro(new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 1)));
        Assert.False(RetroPoRule.IsRetro(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1)));
    }
}
