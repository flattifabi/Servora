using System.Net.Mail;

namespace Servora.Application.Configuration;

/// <summary>Validates SMTP options before they are stored or used.</summary>
public static class SmtpSettingsValidator
{
    /// <summary>The maximum accepted password length.</summary>
    public const int MaxPasswordLength = 1024;

    /// <summary>Validates an SMTP configuration.</summary>
    /// <param name="update">The configuration to validate.</param>
    /// <param name="passwordStored">Whether a password is already stored (relevant when the update keeps it).</param>
    /// <returns>The validation errors; empty when valid.</returns>
    public static IReadOnlyList<string> Validate(SmtpSettingsUpdate update, bool passwordStored = false)
    {
        ArgumentNullException.ThrowIfNull(update);
        var errors = new List<string>();
        var host = update.Host?.Trim() ?? string.Empty;
        var from = update.FromAddress?.Trim() ?? string.Empty;
        var userName = update.UserName?.Trim() ?? string.Empty;

        if (update.Enabled && host.Length == 0)
        {
            errors.Add("The SMTP server is required when e-mail delivery is enabled.");
        }
        else if (host.Length > 0 && (host.Length > 253 || Uri.CheckHostName(host) == UriHostNameType.Unknown))
        {
            errors.Add("The SMTP server must be a valid host name or IP address.");
        }

        if (update.Port is < 1 or > 65535)
        {
            errors.Add("The port must be between 1 and 65535.");
        }
        else if (update.Port == 465)
        {
            errors.Add("Port 465 uses implicit TLS (SMTPS), which is not supported. Use port 587 with STARTTLS.");
        }

        if (!Enum.IsDefined(update.Security))
        {
            errors.Add("The security mode is invalid.");
        }

        if (update.Enabled && from.Length == 0)
        {
            errors.Add("The sender address is required when e-mail delivery is enabled.");
        }
        else if (from.Length > 0 && !IsValidAddress(from))
        {
            errors.Add("The sender address must be a valid e-mail address.");
        }

        if ((update.FromName?.Length ?? 0) > 120 || ContainsLineBreak(update.FromName))
        {
            errors.Add("The sender name must be a single line of at most 120 characters.");
        }

        if (userName.Length > 256 || ContainsLineBreak(userName))
        {
            errors.Add("The user name must be a single line of at most 256 characters.");
        }

        if (update.NewPassword is { Length: > MaxPasswordLength })
        {
            errors.Add($"The password must not exceed {MaxPasswordLength} characters.");
        }

        var sendsCredentials = userName.Length > 0 && (!string.IsNullOrEmpty(update.NewPassword) || (passwordStored && !update.ClearPassword));
        if (update.Security == SmtpSecurityMode.None && sendsCredentials && !IsLoopbackHost(host))
        {
            errors.Add("Credentials must not be sent over an unencrypted connection. Enable STARTTLS or use an anonymous relay.");
        }

        if (update.TimeoutSeconds is < 5 or > 300)
        {
            errors.Add("The timeout must be between 5 and 300 seconds.");
        }

        if (update.MaxAttempts is < 1 or > 20)
        {
            errors.Add("The number of delivery attempts must be between 1 and 20.");
        }

        return errors;
    }

    /// <summary>Checks whether a string is a single, plain e-mail address.</summary>
    /// <param name="address">The address.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValidAddress(string? address) =>
        !string.IsNullOrWhiteSpace(address)
        && address.Length <= 254
        && !ContainsLineBreak(address)
        && MailAddress.TryCreate(address.Trim(), out var parsed)
        && string.Equals(parsed.Address, address.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool ContainsLineBreak(string? value) => value is not null && (value.Contains('\r') || value.Contains('\n'));

    private static bool IsLoopbackHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "::1" || host == "[::1]";
}
