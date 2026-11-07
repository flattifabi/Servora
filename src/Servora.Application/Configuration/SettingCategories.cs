namespace Servora.Application.Configuration;

/// <summary>Well-known setting categories (tabs of the system settings page).</summary>
public static class SettingCategories
{
    /// <summary>General instance settings.</summary>
    public const string General = "General";

    /// <summary>Product name, company name, logo and accent color.</summary>
    public const string Branding = "Branding";

    /// <summary>Time zone, culture and date formats.</summary>
    public const string Localization = "Localization";

    /// <summary>SMTP e-mail delivery.</summary>
    public const string Email = "E-mail";

    /// <summary>Security related information and settings.</summary>
    public const string Security = "Security";

    /// <summary>Feature flags (<c>features.*</c>).</summary>
    public const string Features = "Features";
}
