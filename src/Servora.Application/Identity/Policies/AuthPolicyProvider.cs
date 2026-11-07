namespace Servora.Application.Identity.Policies;

/// <summary>Default <see cref="IAuthPolicyProvider"/> reading <see cref="AuthPolicySettings"/> from the system properties.</summary>
/// <param name="store">The identity settings store.</param>
public sealed class AuthPolicyProvider(IdentitySettingsStore store) : IAuthPolicyProvider
{
    /// <inheritdoc />
    public async Task<AuthPolicySettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await store.GetAsync(AuthPolicySettings.SettingKey, () => new AuthPolicySettings(), cancellationToken);

        // Never let a corrupted value weaken the policy below the defaults' safety limits.
        return settings.Validate() is null ? settings : new AuthPolicySettings();
    }

    /// <inheritdoc />
    public async Task<PasswordPolicy> GetPasswordPolicyAsync(CancellationToken cancellationToken = default) =>
        (await GetAsync(cancellationToken)).ToPasswordPolicy();
}
