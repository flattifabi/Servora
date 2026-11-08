using Servora.Application.Configuration;
using Servora.Web.Services;

namespace Servora.Tests.Unit.Host;

/// <summary>The branding snapshot used for page titles and the accent color only embeds validated values.</summary>
public sealed class BrandingStateTests
{
    [Fact]
    public void Starts_with_the_default_branding()
    {
        var state = new BrandingState();

        Assert.Equal(CoreSettingDefinitionProvider.DefaultProductName, state.ProductName);
        Assert.Null(state.CustomAccentColor);
    }

    [Fact]
    public async Task Refresh_updates_product_name_and_accent()
    {
        var state = new BrandingState();

        await state.RefreshAsync(new StubBrandingService(new BrandingInfo("Acme Service Hub", "Acme", "#12AB34", null)));

        Assert.Equal("Acme Service Hub", state.ProductName);
        Assert.Equal("#12ab34", state.CustomAccentColor);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#123")]
    [InlineData("#12345g")]
    [InlineData("#123456;}body{display:none")]
    [InlineData(CoreSettingDefinitionProvider.DefaultAccentColor)]
    public async Task Invalid_or_default_accent_is_not_embedded(string accent)
    {
        var state = new BrandingState();

        await state.RefreshAsync(new StubBrandingService(new BrandingInfo("Servora", string.Empty, accent, null)));

        Assert.Null(state.CustomAccentColor);
    }
}
