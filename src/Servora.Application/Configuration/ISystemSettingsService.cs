using Servora.Application.Common;

namespace Servora.Application.Configuration;

/// <summary>
/// Typed access to system settings (<c>sys_properties</c>) with defaults, categories, validation, caching and audited
/// change history. Changes take effect immediately (no restart) unless a definition says otherwise.
/// </summary>
/// <remarks>
/// The typed getters are intended for backend use by every module and are not permission checked: settings never
/// contain secrets (those live in <c>ISecretStore</c>). Listing for administration and every mutation require
/// <c>admin.settings.manage</c>.
/// </remarks>
public interface ISystemSettingsService
{
    /// <summary>Gets all definitions contributed by <see cref="ISettingDefinitionProvider"/>s (custom feature flags are not included).</summary>
    IReadOnlyList<SettingDefinition> Definitions { get; }

    /// <summary>Finds the definition of a key.</summary>
    /// <param name="key">The setting key.</param>
    /// <returns>The definition or <see langword="null"/>.</returns>
    SettingDefinition? FindDefinition(string key);

    /// <summary>Gets the effective value of a setting (stored value or default).</summary>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The value; an empty string when neither a value nor a default exists.</returns>
    Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Gets the effective value of a setting converted to <typeparamref name="T"/>.</summary>
    /// <typeparam name="T"><see cref="string"/>, <see cref="bool"/>, <see cref="int"/>, <see cref="long"/> or an enum.</typeparam>
    /// <param name="key">The setting key.</param>
    /// <param name="fallback">The value returned when the setting cannot be converted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed value.</returns>
    Task<T> GetAsync<T>(string key, T fallback, CancellationToken cancellationToken = default);

    /// <summary>Gets the effective values of all settings whose key starts with a prefix.</summary>
    /// <param name="prefix">The key prefix, for example <c>email.</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The values by key.</returns>
    Task<IReadOnlyDictionary<string, string>> GetValuesAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>Lists the visible settings of a category for administration.</summary>
    /// <param name="category">The category, or <see langword="null"/> for all categories.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings ordered by category and order.</returns>
    Task<Result<IReadOnlyList<SettingItem>>> ListAsync(string? category = null, CancellationToken cancellationToken = default);

    /// <summary>Validates and stores a single value.</summary>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The raw value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SetAsync(string key, string? value, CancellationToken cancellationToken = default);

    /// <summary>Validates all values first and stores them in one transaction (all or nothing).</summary>
    /// <param name="values">The raw values by key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result; on validation failure nothing is stored.</returns>
    Task<Result> SetManyAsync(IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default);

    /// <summary>Removes the stored value so the default applies again.</summary>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> ResetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Creates a custom feature flag (<c>features.&lt;name&gt;</c>).</summary>
    /// <param name="key">The full key, for example <c>features.new_dashboard</c>.</param>
    /// <param name="description">An optional description.</param>
    /// <param name="enabled">The initial state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> CreateFeatureFlagAsync(string key, string? description, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>Deletes a custom feature flag. Built-in flags cannot be deleted (only reset).</summary>
    /// <param name="key">The flag key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteFeatureFlagAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Reads the most recent configuration changes from the audit log.</summary>
    /// <param name="key">An optional key to filter by.</param>
    /// <param name="take">The maximum number of entries (1..200).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The changes, newest first.</returns>
    Task<Result<IReadOnlyList<SettingChange>>> GetHistoryAsync(string? key = null, int take = 50, CancellationToken cancellationToken = default);
}
