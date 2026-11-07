namespace Servora.Application.Configuration;

/// <summary>Built-in setting definitions of the platform (general, branding, localization, e-mail and feature flags).</summary>
public sealed class CoreSettingDefinitionProvider : ISettingDefinitionProvider
{
    /// <summary>The default accent color of the design system.</summary>
    public const string DefaultAccentColor = "#7c6cff";

    /// <summary>The default product name.</summary>
    public const string DefaultProductName = "Servora";

    private static readonly SettingDefinition[] s_definitions =
    [
        // General
        new(SystemSettingKeys.BaseUrl, SettingCategories.General, "Public URL", SettingValueType.Url, string.Empty)
        {
            Description = "The address users reach this instance at. Used for links in e-mails and API documentation.",
            Placeholder = "https://servora.example.com",
            Order = 10,
        },
        new(SystemSettingKeys.SupportEmail, SettingCategories.General, "Support contact", SettingValueType.Email, string.Empty)
        {
            Description = "Shown to users who need help with their account.",
            Placeholder = "servicedesk@example.com",
            Order = 20,
        },
        new(SystemSettingKeys.DefaultPageSize, SettingCategories.General, "Rows per page", SettingValueType.Integer, "25")
        {
            Description = "Default number of rows shown in lists.",
            Min = 10,
            Max = 200,
            Order = 30,
        },
        new(SystemSettingKeys.LoginBanner, SettingCategories.General, "Sign-in notice", SettingValueType.MultilineText, string.Empty)
        {
            Description = "Optional legal or usage notice shown on the sign-in page. Plain text only.",
            MaxLength = 1000,
            Order = 40,
        },

        // Branding
        new(SystemSettingKeys.ProductName, SettingCategories.Branding, "Product name", SettingValueType.String, DefaultProductName)
        {
            Description = "Shown in the navigation, browser titles and e-mails.",
            Required = true,
            MaxLength = 60,
            Order = 10,
        },
        new(SystemSettingKeys.CompanyName, SettingCategories.Branding, "Company name", SettingValueType.String, string.Empty)
        {
            Description = "The organisation operating this instance.",
            MaxLength = 120,
            Order = 20,
        },
        new(SystemSettingKeys.AccentColor, SettingCategories.Branding, "Accent color", SettingValueType.Color, DefaultAccentColor)
        {
            Description = "Primary color for buttons, links and highlights.",
            Required = true,
            Order = 30,
        },
        new(SystemSettingKeys.LogoContentType, SettingCategories.Branding, "Logo content type", SettingValueType.String, string.Empty) { IsHidden = true, MaxLength = 64 },
        new(SystemSettingKeys.LogoVersion, SettingCategories.Branding, "Logo version", SettingValueType.String, string.Empty) { IsHidden = true, MaxLength = 64 },

        // Localization
        new(SystemSettingKeys.TimeZone, SettingCategories.Localization, "Time zone", SettingValueType.TimeZone, "UTC")
        {
            Description = "Default time zone for users without a personal preference, schedules and reports.",
            Required = true,
            MaxLength = 100,
            Order = 10,
        },
        new(SystemSettingKeys.Culture, SettingCategories.Localization, "Regional format", SettingValueType.Choice, "en-US")
        {
            Description = "Default culture used to format numbers and dates.",
            Choices = ["en-US", "en-GB", "de-DE", "de-CH", "de-AT", "fr-FR", "es-ES", "it-IT", "nl-NL", "pt-BR", "ja-JP"],
            Required = true,
            Order = 20,
        },
        new(SystemSettingKeys.DateFormat, SettingCategories.Localization, "Date format", SettingValueType.Choice, "yyyy-MM-dd")
        {
            Choices = ["yyyy-MM-dd", "dd.MM.yyyy", "dd/MM/yyyy", "MM/dd/yyyy"],
            Required = true,
            Order = 30,
        },
        new(SystemSettingKeys.TimeFormat, SettingCategories.Localization, "Time format", SettingValueType.Choice, "HH:mm")
        {
            Choices = ["HH:mm", "hh:mm tt"],
            Required = true,
            Order = 40,
        },
        new(SystemSettingKeys.FirstDayOfWeek, SettingCategories.Localization, "First day of week", SettingValueType.Choice, "Monday")
        {
            Choices = ["Monday", "Sunday", "Saturday"],
            Required = true,
            Order = 50,
        },

        // E-mail (edited on the dedicated e-mail panel)
        new(SystemSettingKeys.EmailEnabled, SettingCategories.Email, "E-mail delivery enabled", SettingValueType.Boolean, "false") { Order = 10 },
        new(SystemSettingKeys.SmtpHost, SettingCategories.Email, "SMTP server", SettingValueType.String, string.Empty) { MaxLength = 253, Order = 20 },
        new(SystemSettingKeys.SmtpPort, SettingCategories.Email, "Port", SettingValueType.Integer, "587") { Min = 1, Max = 65535, Order = 30 },
        new(SystemSettingKeys.SmtpSecurity, SettingCategories.Email, "Security", SettingValueType.Choice, nameof(SmtpSecurityMode.StartTls))
        {
            Choices = [nameof(SmtpSecurityMode.StartTls), nameof(SmtpSecurityMode.None)],
            Required = true,
            Order = 40,
        },
        new(SystemSettingKeys.SmtpUserName, SettingCategories.Email, "User name", SettingValueType.String, string.Empty) { MaxLength = 256, Order = 50 },
        new(SystemSettingKeys.FromAddress, SettingCategories.Email, "Sender address", SettingValueType.Email, string.Empty) { Order = 60 },
        new(SystemSettingKeys.FromName, SettingCategories.Email, "Sender name", SettingValueType.String, DefaultProductName) { MaxLength = 120, Order = 70 },
        new(SystemSettingKeys.SmtpTimeoutSeconds, SettingCategories.Email, "Timeout (seconds)", SettingValueType.Integer, "30") { Min = 5, Max = 300, Order = 80 },
        new(SystemSettingKeys.MaxDeliveryAttempts, SettingCategories.Email, "Delivery attempts", SettingValueType.Integer, "6") { Min = 1, Max = 20, Order = 90 },

        // Feature flags
        new(SystemSettingKeys.FeaturePortal, SettingCategories.Features, "Self-service portal", SettingValueType.Boolean, "true")
        {
            Description = "Employees can search knowledge, order catalog items and track their requests.",
            Order = 10,
        },
        new(SystemSettingKeys.FeatureAiAssistant, SettingCategories.Features, "AI assistant", SettingValueType.Boolean, "true")
        {
            Description = "In-app assistant backed by the configured AI providers.",
            Order = 20,
        },
        new(SystemSettingKeys.FeatureMcpServer, SettingCategories.Features, "MCP server", SettingValueType.Boolean, "false")
        {
            Description = "Expose scoped tools to AI agents through the Model Context Protocol endpoint /mcp.",
            Order = 30,
        },
        new(SystemSettingKeys.FeatureRestApi, SettingCategories.Features, "REST API", SettingValueType.Boolean, "true")
        {
            Description = "Allow integrations to call /api/v1 with API keys.",
            Order = 40,
        },
        new(SystemSettingKeys.FeatureKnowledgeFeedback, SettingCategories.Features, "Knowledge feedback", SettingValueType.Boolean, "true")
        {
            Description = "Readers can rate and comment on knowledge articles.",
            Order = 50,
        },
        new(SystemSettingKeys.FeatureEmailNotifications, SettingCategories.Features, "E-mail notifications", SettingValueType.Boolean, "true")
        {
            Description = "Send notification e-mails for record events (requires a working SMTP configuration).",
            Order = 60,
        },
    ];

    /// <summary>Gets the built-in definitions.</summary>
    public static IReadOnlyList<SettingDefinition> All => s_definitions;

    /// <inheritdoc />
    public IEnumerable<SettingDefinition> GetDefinitions() => s_definitions;
}
