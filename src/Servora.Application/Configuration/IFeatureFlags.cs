namespace Servora.Application.Configuration;

/// <summary>
/// Reads feature flags (<c>features.*</c> settings). Modules use it to switch features on and off at runtime without a
/// restart, for example <c>await features.IsEnabledAsync(SystemSettingKeys.FeatureMcpServer)</c>.
/// </summary>
public interface IFeatureFlags
{
    /// <summary>Checks whether a feature is enabled.</summary>
    /// <param name="flag">The flag key, with or without the <c>features.</c> prefix.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when enabled; unknown flags are disabled.</returns>
    Task<bool> IsEnabledAsync(string flag, CancellationToken cancellationToken = default);
}
