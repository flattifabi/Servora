using Servora.Application.Configuration;
using Servora.Application.Security;

namespace Servora.Infrastructure.Configuration;

/// <summary>
/// Reads the SMTP configuration from system settings and the password from <see cref="ISecretStore"/>. Backend use only
/// (outbox processor, test e-mail); never exposed to UI components.
/// </summary>
/// <param name="settings">The settings service.</param>
/// <param name="secrets">The secret store.</param>
public sealed class SmtpOptionsProvider(ISystemSettingsService settings, ISecretStore secrets)
{
    /// <summary>Reads the settings shown in the administration UI (without password).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings.</returns>
    public async Task<SmtpSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => new()
    {
        Enabled = await settings.GetAsync(SystemSettingKeys.EmailEnabled, false, cancellationToken),
        Host = await settings.GetStringAsync(SystemSettingKeys.SmtpHost, cancellationToken),
        Port = await settings.GetAsync(SystemSettingKeys.SmtpPort, 587, cancellationToken),
        Security = await settings.GetAsync(SystemSettingKeys.SmtpSecurity, SmtpSecurityMode.StartTls, cancellationToken),
        UserName = await settings.GetStringAsync(SystemSettingKeys.SmtpUserName, cancellationToken),
        FromAddress = await settings.GetStringAsync(SystemSettingKeys.FromAddress, cancellationToken),
        FromName = await settings.GetStringAsync(SystemSettingKeys.FromName, cancellationToken),
        TimeoutSeconds = await settings.GetAsync(SystemSettingKeys.SmtpTimeoutSeconds, 30, cancellationToken),
        MaxAttempts = await settings.GetAsync(SystemSettingKeys.MaxDeliveryAttempts, 6, cancellationToken),
        HasPassword = await secrets.ExistsAsync(SystemSettingKeys.SmtpPasswordSecret, cancellationToken),
    };

    /// <summary>Resolves the connection options including the decrypted password.</summary>
    /// <param name="requireEnabled">Whether to return <see langword="null"/> when e-mail delivery is disabled.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The options, or <see langword="null"/> when delivery is disabled or the configuration is incomplete.</returns>
    public async Task<SmtpConnectionOptions?> ResolveAsync(bool requireEnabled = true, CancellationToken cancellationToken = default)
    {
        var current = await GetSettingsAsync(cancellationToken);
        if ((requireEnabled && !current.Enabled) || !current.IsComplete)
        {
            return null;
        }

        return new SmtpConnectionOptions
        {
            Host = current.Host,
            Port = current.Port,
            Security = current.Security,
            UserName = current.UserName,
            Password = current.HasPassword && current.UserName.Length > 0 ? await secrets.GetAsync(SystemSettingKeys.SmtpPasswordSecret, cancellationToken) : null,
            FromAddress = current.FromAddress,
            FromName = current.FromName,
            Timeout = TimeSpan.FromSeconds(Math.Clamp(current.TimeoutSeconds, 5, 300)),
        };
    }
}
