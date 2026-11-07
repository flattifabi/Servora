namespace Servora.Application.Configuration;

/// <summary>The SMTP configuration as shown in the administration UI. The password is never included.</summary>
public sealed record SmtpSettings
{
    /// <summary>Gets a value indicating whether outgoing e-mail is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets the SMTP server host name.</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>Gets the SMTP server port.</summary>
    public int Port { get; init; } = 587;

    /// <summary>Gets the transport security.</summary>
    public SmtpSecurityMode Security { get; init; } = SmtpSecurityMode.StartTls;

    /// <summary>Gets the user name; empty for anonymous relay.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the sender address.</summary>
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>Gets the sender display name.</summary>
    public string FromName { get; init; } = string.Empty;

    /// <summary>Gets the operation timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Gets the maximum number of delivery attempts per message.</summary>
    public int MaxAttempts { get; init; } = 6;

    /// <summary>Gets a value indicating whether a password is stored (the value itself is write-only).</summary>
    public bool HasPassword { get; init; }

    /// <summary>Gets a value indicating whether the minimum configuration for delivery is present.</summary>
    public bool IsComplete => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}
