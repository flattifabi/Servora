using Servora.Application.Configuration;

namespace Servora.Application.Portal;

/// <summary>
/// Setting definitions of the self-service portal (hero texts, frequently searched terms, support card, footer links,
/// currency). Edited on the portal settings page (<c>portal.manage</c>); values are plain text and rendered encoded.
/// </summary>
public sealed class PortalSettingDefinitionProvider : ISettingDefinitionProvider
{
    /// <summary>The supported currencies.</summary>
    public static readonly IReadOnlyList<string> Currencies = ["EUR", "USD", "GBP", "CHF"];

    private static readonly SettingDefinition[] s_definitions =
    [
        Text(PortalSettingKeys.HeroGreeting, "Greeting", "Hello {name}, nice to have you here.", 120, 10, "Shown above the headline. {name} is replaced by the user's first name."),
        Text(PortalSettingKeys.HeroTitle, "Headline", "Good work starts here.", 80, 20, null, required: true),
        Text(PortalSettingKeys.HeroSubtitle, "Subline", "Everything for your working day. Find and order IT services in one place.", 200, 30, null),
        Text(PortalSettingKeys.SearchPlaceholder, "Search placeholder", "What do you need? Search for services, software and more …", 120, 40, null),
        new(PortalSettingKeys.FrequentSearches, PortalSettingKeys.Category, "Frequently searched", SettingValueType.MultilineText, "Laptop\nMicrosoft 365\nVPN access")
        {
            Description = "Search terms offered below the search box, one per line (max. 6).",
            MaxLength = 400,
            Order = 50,
        },
        Text(PortalSettingKeys.HeroImageCaption, "Image caption", "Your next upgrade is waiting.", 80, 60, "Small caption on the hero image. Leave empty to hide it."),
        new(PortalSettingKeys.HeroImageType, PortalSettingKeys.Category, "Hero image content type", SettingValueType.String, string.Empty) { IsHidden = true, MaxLength = 64 },
        new(PortalSettingKeys.HeroImageVersion, PortalSettingKeys.Category, "Hero image version", SettingValueType.String, string.Empty) { IsHidden = true, MaxLength = 64 },
        Text(PortalSettingKeys.SupportEyebrow, "Support eyebrow", "We are here for you", 60, 100, null),
        Text(PortalSettingKeys.SupportTitle, "Support title", "Questions?", 80, 110, null, required: true),
        Text(PortalSettingKeys.SupportText, "Support text", "Our IT team helps you find the right service.", 240, 120, null),
        Text(PortalSettingKeys.SupportLinkText, "Support button", "Contact IT support", 60, 130, null),
        Text(PortalSettingKeys.SupportUrl, "Support link", "/portal/report", 500, 140, "Site-relative path or http(s) URL."),
        Text(PortalSettingKeys.SupportHours, "Support hours", "Mon–Fri, 8:00 am – 6:00 pm · Ext. 1234", 120, 150, null),
        Text(PortalSettingKeys.FooterTagline, "Footer tagline", "Your workplace. Simply organised.", 120, 200, null),
        Text(PortalSettingKeys.ContactUrl, "Contact link", "/portal/report", 500, 210, "Leave empty to hide the link."),
        Text(PortalSettingKeys.PrivacyUrl, "Privacy link", string.Empty, 500, 220, "Leave empty to hide the link."),
        Text(PortalSettingKeys.AccessibilityUrl, "Accessibility link", string.Empty, 500, 230, "Leave empty to hide the link."),
        new(PortalSettingKeys.Currency, PortalSettingKeys.Category, "Currency", SettingValueType.Choice, "EUR")
        {
            Description = "Currency of catalog prices.",
            Choices = Currencies,
            Required = true,
            Order = 300,
        },
    ];

    /// <inheritdoc />
    public IEnumerable<SettingDefinition> GetDefinitions() => s_definitions;

    private static SettingDefinition Text(string key, string label, string defaultValue, int maxLength, int order, string? description, bool required = false) =>
        new(key, PortalSettingKeys.Category, label, SettingValueType.String, defaultValue)
        {
            Description = description,
            MaxLength = maxLength,
            Order = order,
            Required = required,
        };
}
