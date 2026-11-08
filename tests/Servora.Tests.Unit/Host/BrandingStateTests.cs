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
    [InlineData("#7C6CFF")]
    public async Task Invalid_default_or_retired_default_accent_is_not_embedded(string accent)
    {
        var state = new BrandingState();

        await state.RefreshAsync(new StubBrandingService(new BrandingInfo("Servora", string.Empty, accent, null)));

        Assert.Null(state.CustomAccentColor);
        Assert.Null(state.ThemeStyle);
    }

    [Fact]
    public async Task Theme_style_injects_brand_primary_and_readable_contrast()
    {
        var state = new BrandingState();

        await state.RefreshAsync(new StubBrandingService(new BrandingInfo("Servora", string.Empty, "#1D4F91", null)));

        Assert.Equal(":root { --brand: #1d4f91; --primary: #1d4f91; --primary-contrast: #ffffff; }", state.ThemeStyle);
    }

    [Theory]
    [InlineData("#1b4535", BrandingState.LightContrastColor)]
    [InlineData("#000000", BrandingState.LightContrastColor)]
    [InlineData("#1d4f91", BrandingState.LightContrastColor)]
    [InlineData("#ffffff", BrandingState.DarkContrastColor)]
    [InlineData("#f5d90a", BrandingState.DarkContrastColor)]
    [InlineData("#22c55e", BrandingState.DarkContrastColor)]
    [InlineData("not-a-color", BrandingState.LightContrastColor)]
    public void Contrast_color_picks_the_more_readable_text_color(string background, string expected)
    {
        Assert.Equal(expected, BrandingState.ContrastColorFor(background));
    }

    [Fact]
    public void Relative_luminance_matches_wcag_reference_values()
    {
        Assert.Equal(0d, BrandingState.RelativeLuminance("#000000"), 4);
        Assert.Equal(1d, BrandingState.RelativeLuminance("#ffffff"), 4);
        Assert.Equal(0.2126, BrandingState.RelativeLuminance("#ff0000"), 4);
    }
}
