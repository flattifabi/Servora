namespace Servora.Application.Portal;

/// <summary>
/// The configurable texts and links of the self-service portal (read model of the portal pages and edit model of the
/// portal settings page). Values are plain text; links are checked with <see cref="PortalUrlPolicy"/> on save and again
/// when rendered.
/// </summary>
public sealed class PortalSettingsModel
{
    /// <summary>The maximum number of frequently searched terms.</summary>
    public const int MaxFrequentSearches = 6;

    /// <summary>Gets or sets the greeting (<c>{name}</c> is replaced by the first name).</summary>
    public string HeroGreeting { get; set; } = string.Empty;

    /// <summary>Gets or sets the hero headline.</summary>
    public string HeroTitle { get; set; } = string.Empty;

    /// <summary>Gets or sets the hero subline.</summary>
    public string HeroSubtitle { get; set; } = string.Empty;

    /// <summary>Gets or sets the search placeholder.</summary>
    public string SearchPlaceholder { get; set; } = string.Empty;

    /// <summary>Gets or sets the frequently searched terms, one per line.</summary>
    public string FrequentSearches { get; set; } = string.Empty;

    /// <summary>Gets or sets the caption chip of the hero image.</summary>
    public string HeroImageCaption { get; set; } = string.Empty;

    /// <summary>Gets or sets the relative URL of the uploaded hero image (read only; <see langword="null"/> = illustration).</summary>
    public string? HeroImageUrl { get; set; }

    /// <summary>Gets or sets the support card eyebrow.</summary>
    public string SupportEyebrow { get; set; } = string.Empty;

    /// <summary>Gets or sets the support card title.</summary>
    public string SupportTitle { get; set; } = string.Empty;

    /// <summary>Gets or sets the support card text.</summary>
    public string SupportText { get; set; } = string.Empty;

    /// <summary>Gets or sets the support button text.</summary>
    public string SupportLinkText { get; set; } = string.Empty;

    /// <summary>Gets or sets the support button link.</summary>
    public string SupportUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the support hours / phone line.</summary>
    public string SupportHours { get; set; } = string.Empty;

    /// <summary>Gets or sets the footer tagline.</summary>
    public string FooterTagline { get; set; } = string.Empty;

    /// <summary>Gets or sets the footer "Contact" link.</summary>
    public string ContactUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the footer "Privacy" link.</summary>
    public string PrivacyUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the footer "Accessibility" link.</summary>
    public string AccessibilityUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the currency code (EUR, USD, GBP, CHF).</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets the frequently searched terms as a list.</summary>
    public IReadOnlyList<string> FrequentSearchTerms =>
        FrequentSearches.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length is > 0 and <= 40)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxFrequentSearches)
            .ToList();

    /// <summary>Formats the greeting for a user.</summary>
    /// <param name="firstName">The first name.</param>
    /// <returns>The greeting text (plain text, to be encoded when rendered).</returns>
    public string GreetingFor(string? firstName) =>
        HeroGreeting.Replace("{name}", string.IsNullOrWhiteSpace(firstName) ? "there" : firstName.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns a link when it may be rendered, otherwise <see langword="null"/>.</summary>
    /// <param name="url">The configured link.</param>
    /// <returns>The safe link or <see langword="null"/>.</returns>
    public static string? SafeLink(string? url) => PortalUrlPolicy.IsAllowed(url) ? url!.Trim() : null;

    /// <summary>Creates a copy for editing.</summary>
    /// <returns>The copy.</returns>
    public PortalSettingsModel Clone() => (PortalSettingsModel)MemberwiseClone();
}
