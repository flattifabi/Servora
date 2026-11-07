namespace Servora.Application.Configuration;

/// <summary>Input for saving the SMTP configuration. Mutable so it can be bound to a form.</summary>
public sealed class SmtpSettingsUpdate
{
    /// <summary>Gets or sets a value indicating whether outgoing e-mail is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the SMTP server host name.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMTP server port.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Gets or sets the transport security.</summary>
    public SmtpSecurityMode Security { get; set; } = SmtpSecurityMode.StartTls;

    /// <summary>Gets or sets the user name; empty for anonymous relay.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets a new password. <see langword="null"/> or empty keeps the stored password (write-only field).</summary>
    public string? NewPassword { get; set; }

    /// <summary>Gets or sets a value indicating whether the stored password should be removed.</summary>
    public bool ClearPassword { get; set; }

    /// <summary>Gets or sets the sender address.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Gets or sets the sender display name.</summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>Gets or sets the operation timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Gets or sets the maximum number of delivery attempts per message.</summary>
    public int MaxAttempts { get; set; } = 6;

    /// <summary>Creates an update model pre-filled from the current settings.</summary>
    /// <param name="settings">The current settings.</param>
    /// <returns>The update model (without password).</returns>
    public static SmtpSettingsUpdate From(SmtpSettings settings) => new()
    {
        Enabled = settings.Enabled,
        Host = settings.Host,
        Port = settings.Port,
        Security = settings.Security,
        UserName = settings.UserName,
        FromAddress = settings.FromAddress,
        FromName = settings.FromName,
        TimeoutSeconds = settings.TimeoutSeconds,
        MaxAttempts = settings.MaxAttempts,
    };
}
