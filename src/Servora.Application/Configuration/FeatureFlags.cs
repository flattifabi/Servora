namespace Servora.Application.Configuration;

/// <summary><see cref="IFeatureFlags"/> backed by <see cref="ISystemSettingsService"/>.</summary>
/// <param name="settings">The settings service.</param>
public sealed class FeatureFlags(ISystemSettingsService settings) : IFeatureFlags
{
    /// <inheritdoc />
    public Task<bool> IsEnabledAsync(string flag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flag);
        var key = flag.StartsWith(SystemSettingKeys.FeaturePrefix, StringComparison.Ordinal) ? flag : SystemSettingKeys.FeaturePrefix + flag;
        return settings.GetAsync(key, false, cancellationToken);
    }
}
