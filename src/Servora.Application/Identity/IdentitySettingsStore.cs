using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Common;
using Servora.Domain.Configuration;

namespace Servora.Application.Identity;

/// <summary>
/// Persists identity configuration objects (authentication policy, directory settings) as JSON system properties
/// (<see cref="SystemSetting"/>, category <c>Identity</c>). Values are cached for a short period and the cache is
/// dropped on every save. Secrets are never stored here (they go to <c>ISecretStore</c>).
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="cache">The memory cache.</param>
public sealed class IdentitySettingsStore(IAppDbContextFactory dbFactory, IMemoryCache cache)
{
    /// <summary>The system property category used for identity settings.</summary>
    public const string Category = "Identity";

    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    /// <summary>Reads a settings object, falling back to defaults when it is missing or unreadable.</summary>
    /// <typeparam name="T">The settings type.</typeparam>
    /// <param name="key">The system property key.</param>
    /// <param name="defaults">Factory for the default value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings (a fresh copy the caller may modify).</returns>
    public async Task<T> GetAsync<T>(string key, Func<T> defaults, CancellationToken cancellationToken = default)
        where T : class
    {
        var cacheKey = CacheKey(key);
        if (!cache.TryGetValue(cacheKey, out string? json))
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            json = await db.Set<SystemSetting>().AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
            cache.Set(cacheKey, json, s_cacheDuration);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return defaults();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, s_json) ?? defaults();
        }
        catch (JsonException)
        {
            return defaults();
        }
    }

    /// <summary>Creates or replaces a settings object.</summary>
    /// <typeparam name="T">The settings type.</typeparam>
    /// <param name="key">The system property key.</param>
    /// <param name="value">The value.</param>
    /// <param name="description">The description shown in the system properties list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the value is stored.</returns>
    public async Task SaveAsync<T>(string key, T value, string description, CancellationToken cancellationToken = default)
        where T : class
    {
        var json = JsonSerializer.Serialize(value, s_json);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var setting = await db.Set<SystemSetting>().FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null)
        {
            setting = new SystemSetting { Key = key, Category = Category, ValueType = "json", Description = description };
            db.Set<SystemSetting>().Add(setting);
        }

        setting.Value = json;
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey(key));
    }

    /// <summary>Checks whether a setting exists.</summary>
    /// <param name="key">The system property key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when present.</returns>
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<SystemSetting>().AnyAsync(s => s.Key == key, cancellationToken);
    }

    private static string CacheKey(string key) => "identity-setting:" + key;
}
