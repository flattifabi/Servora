namespace Servora.Application.Portal;

/// <summary>System setting keys of the self-service portal (see <see cref="PortalSettingDefinitionProvider"/>).</summary>
public static class PortalSettingKeys
{
    /// <summary>The common key prefix.</summary>
    public const string Prefix = "portal.";

    /// <summary>The settings category.</summary>
    public const string Category = "Portal";

    /// <summary>Greeting above the hero headline; <c>{name}</c> is replaced by the user's first name.</summary>
    public const string HeroGreeting = "portal.hero.greeting";

    /// <summary>Hero headline.</summary>
    public const string HeroTitle = "portal.hero.title";

    /// <summary>Line below the hero headline.</summary>
    public const string HeroSubtitle = "portal.hero.subtitle";

    /// <summary>Placeholder of the hero search box.</summary>
    public const string SearchPlaceholder = "portal.hero.search_placeholder";

    /// <summary>Frequently searched terms, one per line.</summary>
    public const string FrequentSearches = "portal.hero.frequent_searches";

    /// <summary>Caption chip on the hero image.</summary>
    public const string HeroImageCaption = "portal.hero.image_caption";

    /// <summary>Content type of the uploaded hero image (managed internally).</summary>
    public const string HeroImageType = "portal.hero.image_type";

    /// <summary>Version of the uploaded hero image (managed internally, cache buster).</summary>
    public const string HeroImageVersion = "portal.hero.image_version";

    /// <summary>Eyebrow of the support card.</summary>
    public const string SupportEyebrow = "portal.support.eyebrow";

    /// <summary>Title of the support card.</summary>
    public const string SupportTitle = "portal.support.title";

    /// <summary>Text of the support card.</summary>
    public const string SupportText = "portal.support.text";

    /// <summary>Button text of the support card.</summary>
    public const string SupportLinkText = "portal.support.link_text";

    /// <summary>Button link of the support card (also used for "Contact IT advice" on order pages).</summary>
    public const string SupportUrl = "portal.support.url";

    /// <summary>Opening hours / phone line of the support card.</summary>
    public const string SupportHours = "portal.support.hours";

    /// <summary>Tagline in the footer.</summary>
    public const string FooterTagline = "portal.footer.tagline";

    /// <summary>Footer "Contact" link.</summary>
    public const string ContactUrl = "portal.footer.contact_url";

    /// <summary>Footer "Privacy" link.</summary>
    public const string PrivacyUrl = "portal.footer.privacy_url";

    /// <summary>Footer "Accessibility" link.</summary>
    public const string AccessibilityUrl = "portal.footer.accessibility_url";

    /// <summary>Currency of catalog prices.</summary>
    public const string Currency = "portal.currency";
}
