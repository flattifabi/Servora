namespace Servora.Application.Configuration;

/// <summary>Keys of the built-in system settings. Keys are lower case and dot separated: <c>&lt;area&gt;.&lt;name&gt;</c>.</summary>
public static class SystemSettingKeys
{
    /// <summary>Prefix of all feature flag keys.</summary>
    public const string FeaturePrefix = "features.";

    /// <summary>Public base URL of the instance used in e-mail links.</summary>
    public const string BaseUrl = "general.base_url";

    /// <summary>Support contact address shown to users.</summary>
    public const string SupportEmail = "general.support_email";

    /// <summary>Message shown on the sign-in page (for example a legal notice).</summary>
    public const string LoginBanner = "general.login_banner";

    /// <summary>Default number of rows per page in lists.</summary>
    public const string DefaultPageSize = "general.default_page_size";

    /// <summary>Product name shown in the navigation, page titles and e-mails.</summary>
    public const string ProductName = "branding.product_name";

    /// <summary>Name of the organisation operating the instance.</summary>
    public const string CompanyName = "branding.company_name";

    /// <summary>Accent color (<c>#rrggbb</c>).</summary>
    public const string AccentColor = "branding.accent_color";

    /// <summary>Internal: content type of the uploaded logo (empty when no logo is set).</summary>
    public const string LogoContentType = "branding.logo_content_type";

    /// <summary>Internal: version stamp of the uploaded logo (cache busting).</summary>
    public const string LogoVersion = "branding.logo_version";

    /// <summary>Default time zone of the instance.</summary>
    public const string TimeZone = "localization.time_zone";

    /// <summary>Default culture used for number and date formatting.</summary>
    public const string Culture = "localization.culture";

    /// <summary>Date display format.</summary>
    public const string DateFormat = "localization.date_format";

    /// <summary>Time display format.</summary>
    public const string TimeFormat = "localization.time_format";

    /// <summary>First day of the week in calendars.</summary>
    public const string FirstDayOfWeek = "localization.first_day_of_week";

    /// <summary>Master switch for outgoing e-mail.</summary>
    public const string EmailEnabled = "email.enabled";

    /// <summary>SMTP server host name.</summary>
    public const string SmtpHost = "email.smtp_host";

    /// <summary>SMTP server port.</summary>
    public const string SmtpPort = "email.smtp_port";

    /// <summary>SMTP transport security (<see cref="SmtpSecurityMode"/>).</summary>
    public const string SmtpSecurity = "email.smtp_security";

    /// <summary>SMTP user name (empty for anonymous relay).</summary>
    public const string SmtpUserName = "email.smtp_username";

    /// <summary>Sender address.</summary>
    public const string FromAddress = "email.from_address";

    /// <summary>Sender display name.</summary>
    public const string FromName = "email.from_name";

    /// <summary>SMTP operation timeout in seconds.</summary>
    public const string SmtpTimeoutSeconds = "email.timeout_seconds";

    /// <summary>Maximum delivery attempts per message before it is marked failed.</summary>
    public const string MaxDeliveryAttempts = "email.max_attempts";

    /// <summary>Name of the secret (in <c>ISecretStore</c>) holding the SMTP password.</summary>
    public const string SmtpPasswordSecret = "email.smtp_password";

    /// <summary>Feature flag: self-service portal.</summary>
    public const string FeaturePortal = "features.self_service_portal";

    /// <summary>Feature flag: AI assistant.</summary>
    public const string FeatureAiAssistant = "features.ai_assistant";

    /// <summary>Feature flag: MCP server endpoint.</summary>
    public const string FeatureMcpServer = "features.mcp_server";

    /// <summary>Feature flag: REST API access with API keys.</summary>
    public const string FeatureRestApi = "features.rest_api";

    /// <summary>Feature flag: knowledge article feedback.</summary>
    public const string FeatureKnowledgeFeedback = "features.knowledge_feedback";

    /// <summary>Feature flag: e-mail notifications.</summary>
    public const string FeatureEmailNotifications = "features.email_notifications";
}
