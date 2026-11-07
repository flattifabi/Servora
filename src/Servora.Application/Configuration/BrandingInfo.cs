namespace Servora.Application.Configuration;

/// <summary>The branding of the instance, safe to show to anonymous users (sign-in page).</summary>
/// <param name="ProductName">The product name.</param>
/// <param name="CompanyName">The company name (may be empty).</param>
/// <param name="AccentColor">The accent color (<c>#rrggbb</c>).</param>
/// <param name="LogoUrl">The relative logo URL including a cache-busting version, or <see langword="null"/> when no logo is set.</param>
public sealed record BrandingInfo(string ProductName, string CompanyName, string AccentColor, string? LogoUrl)
{
    /// <summary>Gets the default branding used before setup or when the database is unavailable.</summary>
    public static BrandingInfo Default { get; } = new(CoreSettingDefinitionProvider.DefaultProductName, string.Empty, CoreSettingDefinitionProvider.DefaultAccentColor, null);

    /// <summary>Gets a value indicating whether a custom logo is configured.</summary>
    public bool HasLogo => LogoUrl is not null;
}
