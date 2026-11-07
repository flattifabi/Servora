namespace Servora.Application.Configuration;

/// <summary>
/// Fully resolved SMTP connection options including the decrypted password. Backend use only: never log, serialize or
/// return this object to the UI. <see cref="ToString"/> deliberately omits the password.
/// </summary>
public sealed class SmtpConnectionOptions
{
    /// <summary>Gets the SMTP server host name.</summary>
    public required string Host { get; init; }

    /// <summary>Gets the SMTP server port.</summary>
    public required int Port { get; init; }

    /// <summary>Gets the transport security.</summary>
    public required SmtpSecurityMode Security { get; init; }

    /// <summary>Gets the user name; empty for anonymous relay.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the decrypted password.</summary>
    public string? Password { get; init; }

    /// <summary>Gets the sender address.</summary>
    public required string FromAddress { get; init; }

    /// <summary>Gets the sender display name.</summary>
    public string FromName { get; init; } = string.Empty;

    /// <summary>Gets the operation timeout.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public override string ToString() => $"{Host}:{Port} ({Security}, user '{UserName}')";
}
