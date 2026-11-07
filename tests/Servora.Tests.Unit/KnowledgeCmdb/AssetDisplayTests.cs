using Servora.Application.Assets;
using Servora.Domain.Assets;

namespace Servora.Tests.Unit.KnowledgeCmdb;

/// <summary>Tests for the asset display rules (warranty classification, license utilisation, money formatting).</summary>
public sealed class AssetDisplayTests
{
    private static readonly DateOnly s_today = new(2026, 6, 1);

    [Theory]
    [InlineData(null, "none")]
    [InlineData(-1, "expired")]
    [InlineData(0, "expiring")]
    [InlineData(90, "expiring")]
    [InlineData(91, "valid")]
    public void WarrantyStatus_ClassifiesRelativeToToday(int? offsetDays, string expected)
    {
        DateOnly? end = offsetDays is { } days ? s_today.AddDays(days) : null;
        Assert.Equal(expected, AssetDisplay.WarrantyStatus(end, s_today));
    }

    [Theory]
    [InlineData(-10, "Expired 10 days ago")]
    [InlineData(-1, "Expired yesterday")]
    [InlineData(0, "Expires today")]
    [InlineData(1, "Expires tomorrow")]
    [InlineData(45, "Expires in 45 days")]
    [InlineData(400, "Valid until 6 Jul 2027")]
    public void WarrantyText_IsHumanReadable(int offsetDays, string expected)
    {
        Assert.Equal(expected, AssetDisplay.WarrantyText(s_today.AddDays(offsetDays), s_today));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(0, 0, null)]
    [InlineData(0, 3, 999)]
    [InlineData(10, 0, 0)]
    [InlineData(10, 9, 90)]
    [InlineData(10, 12, 120)]
    public void LicenseUtilisation_HandlesMissingAndZeroEntitlements(int? entitlements, int? installs, int? expected)
    {
        Assert.Equal(expected, AssetDisplay.LicenseUtilisation(entitlements, installs));
    }

    [Fact]
    public void Money_FormatsInvariantAndCompact()
    {
        Assert.Equal("EUR 1,299.50", AssetDisplay.Money(1299.5m, "eur"));
        Assert.Equal("USD 12.5k", AssetDisplay.Money(12_500m, "USD", compact: true));
        Assert.Equal("EUR 2.25M", AssetDisplay.Money(2_250_000m, "EUR", compact: true));
        Assert.Equal("EUR 9,999.00", AssetDisplay.Money(9_999m, "EUR", compact: true));
    }

    [Fact]
    public void EveryStateAndCategoryHasDisplayValues()
    {
        foreach (var state in Enum.GetValues<AssetState>())
        {
            Assert.False(string.IsNullOrWhiteSpace(AssetDisplay.StateTone(state)));
            Assert.False(string.IsNullOrWhiteSpace(AssetDisplay.TransitionAction(state)));
            Assert.False(string.IsNullOrWhiteSpace(AssetDisplay.TransitionIcon(state)));
        }

        foreach (var category in Enum.GetValues<AssetCategory>())
        {
            Assert.NotEqual(category.ToString(), AssetDisplay.CategoryIcon(category));
            Assert.Equal(category.ToString(), AssetDisplay.Category(category));
        }
    }
}
