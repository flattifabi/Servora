namespace Servora.Application.Portal;

/// <summary>Validates <see cref="PortalSettingsModel"/> input of the portal settings page.</summary>
public static class PortalSettingsValidator
{
    /// <summary>Validates the settings.</summary>
    /// <param name="model">The settings.</param>
    /// <returns><see langword="null"/> when valid; otherwise a user-safe message.</returns>
    public static string? Validate(PortalSettingsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (string.IsNullOrWhiteSpace(model.HeroTitle))
        {
            return "The headline is required.";
        }

        if (string.IsNullOrWhiteSpace(model.SupportTitle))
        {
            return "The support title is required.";
        }

        if (Length(model.HeroGreeting) > 120 || Length(model.HeroTitle) > 80 || Length(model.HeroSubtitle) > 200
            || Length(model.SearchPlaceholder) > 120 || Length(model.HeroImageCaption) > 80)
        {
            return "Greeting (120), headline (80), subline (200), search placeholder (120) or image caption (80) is too long.";
        }

        if (Length(model.SupportEyebrow) > 60 || Length(model.SupportTitle) > 80 || Length(model.SupportText) > 240
            || Length(model.SupportLinkText) > 60 || Length(model.SupportHours) > 120 || Length(model.FooterTagline) > 120)
        {
            return "One of the support or footer texts is too long.";
        }

        var terms = (model.FrequentSearches ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length > PortalSettingsModel.MaxFrequentSearches || terms.Any(t => t.Length > 40))
        {
            return $"Enter at most {PortalSettingsModel.MaxFrequentSearches} frequently searched terms of up to 40 characters.";
        }

        foreach (var (label, url) in new[]
        {
            ("Support link", model.SupportUrl),
            ("Contact link", model.ContactUrl),
            ("Privacy link", model.PrivacyUrl),
            ("Accessibility link", model.AccessibilityUrl),
        })
        {
            if (!string.IsNullOrWhiteSpace(url) && !PortalUrlPolicy.IsAllowed(url))
            {
                return $"{label}: enter a site-relative path (for example /portal/report) or an http(s) URL.";
            }
        }

        if (!PortalSettingDefinitionProvider.Currencies.Contains(model.Currency))
        {
            return "The currency is not supported.";
        }

        return null;
    }

    private static int Length(string? value) => value?.Trim().Length ?? 0;
}
