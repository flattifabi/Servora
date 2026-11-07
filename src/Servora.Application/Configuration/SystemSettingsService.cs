using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Configuration;
using Servora.Domain.Observability;

namespace Servora.Application.Configuration;

/// <summary>
/// Default <see cref="ISystemSettingsService"/>. Stored values are cached process-wide for a few minutes and the cache
/// is invalidated on every change, so changes take effect immediately on this node.
/// </summary>
public sealed class SystemSettingsService : ISystemSettingsService
{
    /// <summary>The audit category used for configuration changes.</summary>
    public const string AuditCategory = "config";

    /// <summary>The upper bound of stored settings loaded into the cache.</summary>
    internal const int MaxStoredSettings = 5000;

    private const string CacheKey = "servora:settings:v1";
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromMinutes(5);

    private readonly IAppDbContextFactory _dbFactory;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly IMemoryCache _cache;
    private readonly IReadOnlyList<SettingDefinition> _definitions;
    private readonly Dictionary<string, SettingDefinition> _byKey;

    /// <summary>Initializes a new instance of the <see cref="SystemSettingsService"/> class.</summary>
    /// <param name="dbFactory">The database context factory.</param>
    /// <param name="currentUser">The current actor.</param>
    /// <param name="audit">The audit service.</param>
    /// <param name="cache">The memory cache.</param>
    /// <param name="providers">The definition providers.</param>
    public SystemSettingsService(
        IAppDbContextFactory dbFactory,
        ICurrentUser currentUser,
        IAuditService audit,
        IMemoryCache cache,
        IEnumerable<ISettingDefinitionProvider> providers)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
        _audit = audit;
        _cache = cache;
        _byKey = new Dictionary<string, SettingDefinition>(StringComparer.Ordinal);
        foreach (var definition in providers.SelectMany(p => p.GetDefinitions()))
        {
            // The first registration of a key wins; later duplicates are ignored.
            _byKey.TryAdd(definition.Key, definition);
        }

        _definitions = _byKey.Values.OrderBy(d => d.Category, StringComparer.Ordinal).ThenBy(d => d.Order).ThenBy(d => d.Key, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<SettingDefinition> Definitions => _definitions;

    /// <inheritdoc />
    public SettingDefinition? FindDefinition(string key) => _byKey.GetValueOrDefault(key);

    /// <summary>Creates the synthetic definition used for an administrator-defined feature flag.</summary>
    /// <param name="key">The flag key.</param>
    /// <param name="description">The stored description.</param>
    /// <returns>The definition.</returns>
    public static SettingDefinition CustomFeatureDefinition(string key, string? description) =>
        new(key, SettingCategories.Features, HumanizeFeatureKey(key), SettingValueType.Boolean, "false")
        {
            Description = description,
            Order = 1000,
        };

    /// <summary>Turns <c>features.new_dashboard</c> into <c>New dashboard</c>.</summary>
    /// <param name="key">The flag key.</param>
    /// <returns>The label.</returns>
    public static string HumanizeFeatureKey(string key)
    {
        var name = key.StartsWith(SystemSettingKeys.FeaturePrefix, StringComparison.Ordinal) ? key[SystemSettingKeys.FeaturePrefix.Length..] : key;
        name = name.Replace('_', ' ').Trim();
        return name.Length == 0 ? key : char.ToUpper(name[0], CultureInfo.InvariantCulture) + name[1..];
    }

    /// <inheritdoc />
    public async Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        var stored = await GetStoredAsync(cancellationToken);
        if (stored.TryGetValue(key, out var value))
        {
            return value.Value;
        }

        return FindDefinition(key)?.DefaultValue ?? string.Empty;
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, T fallback, CancellationToken cancellationToken = default)
    {
        var raw = await GetStringAsync(key, cancellationToken);
        if (SettingValueParser.TryConvert<T>(raw, out var typed))
        {
            return typed;
        }

        // A stored value that no longer converts (for example after a definition change) falls back to the default.
        var defaultValue = FindDefinition(key)?.DefaultValue;
        return SettingValueParser.TryConvert<T>(defaultValue, out var typedDefault) ? typedDefault : fallback;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetValuesAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var stored = await GetStoredAsync(cancellationToken);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in _definitions.Where(d => d.Key.StartsWith(prefix, StringComparison.Ordinal)))
        {
            result[definition.Key] = definition.DefaultValue ?? string.Empty;
        }

        foreach (var (key, value) in stored.Where(s => s.Key.StartsWith(prefix, StringComparison.Ordinal)))
        {
            result[key] = value.Value;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<SettingItem>>> ListAsync(string? category = null, CancellationToken cancellationToken = default)
    {
        if (!await _currentUser.HasPermissionAsync(Permissions.Configuration.ManageSettings, cancellationToken))
        {
            return Result<IReadOnlyList<SettingItem>>.Forbidden();
        }

        var stored = await GetStoredAsync(cancellationToken);
        var items = new List<SettingItem>();
        foreach (var definition in _definitions.Where(d => !d.IsHidden && (category is null || d.Category == category)))
        {
            items.Add(stored.TryGetValue(definition.Key, out var value)
                ? new SettingItem(definition, value.Value, false, value.UpdatedAt, value.UpdatedBy)
                : new SettingItem(definition, definition.DefaultValue ?? string.Empty, true, null, null));
        }

        if (category is null or SettingCategories.Features)
        {
            foreach (var custom in stored.Values.Where(v => v.Key.StartsWith(SystemSettingKeys.FeaturePrefix, StringComparison.Ordinal) && !_byKey.ContainsKey(v.Key)).OrderBy(v => v.Key, StringComparer.Ordinal))
            {
                items.Add(new SettingItem(CustomFeatureDefinition(custom.Key, custom.Description), custom.Value, false, custom.UpdatedAt, custom.UpdatedBy) { IsCustom = true });
            }
        }

        return Result<IReadOnlyList<SettingItem>>.Success(items);
    }

    /// <inheritdoc />
    public Task<Result> SetAsync(string key, string? value, CancellationToken cancellationToken = default) =>
        SetManyAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { [key] = value }, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> SetManyAsync(IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!await _currentUser.HasPermissionAsync(Permissions.Configuration.ManageSettings, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (values.Count == 0)
        {
            return Result.Success();
        }

        if (values.Count > 200)
        {
            return Result.Invalid("Too many settings in one request.");
        }

        var stored = await GetStoredAsync(cancellationToken);
        var normalized = new Dictionary<string, (SettingDefinition Definition, string Value)>(StringComparer.Ordinal);
        var errors = new List<string>();
        foreach (var (key, raw) in values)
        {
            var definition = FindDefinition(key)
                ?? (stored.TryGetValue(key, out var custom) && key.StartsWith(SystemSettingKeys.FeaturePrefix, StringComparison.Ordinal)
                    ? CustomFeatureDefinition(key, custom.Description)
                    : null);
            if (definition is null)
            {
                errors.Add($"Unknown setting '{key}'.");
                continue;
            }

            var result = SettingValueParser.Normalize(definition, raw);
            if (!result.Succeeded)
            {
                errors.Add(result.Error!);
                continue;
            }

            normalized[key] = (definition, result.Value!);
        }

        if (errors.Count > 0)
        {
            return Result.Invalid(string.Join(" ", errors));
        }

        var changes = new List<(string Key, string? Old, string New, bool RequiresRestart)>();
        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var keys = normalized.Keys.ToList();
            var rows = await db.Set<SystemSetting>().Where(s => keys.Contains(s.Key)).ToListAsync(cancellationToken);
            foreach (var (key, (definition, value)) in normalized)
            {
                var row = rows.FirstOrDefault(r => r.Key == key);
                var oldValue = row?.Value ?? definition.DefaultValue ?? string.Empty;
                if (row is null)
                {
                    if (string.Equals(value, definition.DefaultValue ?? string.Empty, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    db.Set<SystemSetting>().Add(new SystemSetting
                    {
                        Key = key,
                        Value = value,
                        Category = definition.Category,
                        Description = Truncate(definition.Description, 2000),
                        ValueType = definition.ValueType.ToString().ToLowerInvariant(),
                        RequiresRestart = definition.RequiresRestart,
                    });
                }
                else
                {
                    if (string.Equals(row.Value, value, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    row.Value = value;
                }

                changes.Add((key, oldValue, value, definition.RequiresRestart));
            }

            if (changes.Count == 0)
            {
                return Result.Success();
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        Invalidate();
        foreach (var change in changes)
        {
            await _audit.WriteAsync(new AuditEntry(AuditCategory, "setting.updated")
            {
                TargetType = "setting",
                TargetId = change.Key,
                TargetName = change.Key,
                Message = $"Setting '{change.Key}' changed{(change.RequiresRestart ? " (restart required)" : string.Empty)}.",
                Details = new { key = change.Key, oldValue = Truncate(change.Old, 500), newValue = Truncate(change.New, 500) },
            }, cancellationToken);
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!await _currentUser.HasPermissionAsync(Permissions.Configuration.ManageSettings, cancellationToken))
        {
            return Result.Forbidden();
        }

        var definition = FindDefinition(key);
        if (definition is null)
        {
            return Result.NotFound($"Unknown setting '{key}'.");
        }

        if (definition.IsHidden)
        {
            return Result.Invalid("This setting is managed by the system.");
        }

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var row = await db.Set<SystemSetting>().FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
            if (row is null)
            {
                return Result.Success();
            }

            db.Set<SystemSetting>().Remove(row);
            await db.SaveChangesAsync(cancellationToken);
            Invalidate();
            await _audit.WriteAsync(new AuditEntry(AuditCategory, "setting.reset")
            {
                TargetType = "setting",
                TargetId = key,
                TargetName = key,
                Message = $"Setting '{key}' reset to its default.",
                Details = new { key, oldValue = Truncate(row.Value, 500), newValue = definition.DefaultValue },
            }, cancellationToken);
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> CreateFeatureFlagAsync(string key, string? description, bool enabled, CancellationToken cancellationToken = default)
    {
        if (!await _currentUser.HasPermissionAsync(Permissions.Configuration.ManageSettings, cancellationToken))
        {
            return Result.Forbidden();
        }

        key = (key ?? string.Empty).Trim().ToLowerInvariant();
        if (!key.StartsWith(SystemSettingKeys.FeaturePrefix, StringComparison.Ordinal))
        {
            key = SystemSettingKeys.FeaturePrefix + key;
        }

        if (!SettingValueParser.IsValidFeatureKey(key))
        {
            return Result.Invalid("The flag name must start with a letter and contain only lower-case letters, digits and underscores (2-61 characters).");
        }

        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (description is { Length: > 500 })
        {
            return Result.Invalid("The description must not exceed 500 characters.");
        }

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            if (_byKey.ContainsKey(key) || await db.Set<SystemSetting>().AnyAsync(s => s.Key == key, cancellationToken))
            {
                return Result.Conflict($"A feature flag named '{key}' already exists.");
            }

            db.Set<SystemSetting>().Add(new SystemSetting
            {
                Key = key,
                Value = enabled ? "true" : "false",
                Category = SettingCategories.Features,
                Description = description,
                ValueType = "boolean",
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        Invalidate();
        await _audit.WriteAsync(new AuditEntry(AuditCategory, "feature.created")
        {
            TargetType = "setting",
            TargetId = key,
            TargetName = key,
            Message = $"Feature flag '{key}' created ({(enabled ? "on" : "off")}).",
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteFeatureFlagAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!await _currentUser.HasPermissionAsync(Permissions.Configuration.ManageSettings, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (_byKey.ContainsKey(key))
        {
            return Result.Invalid("Built-in feature flags cannot be deleted. Reset them to their default instead.");
        }

        if (!key.StartsWith(SystemSettingKeys.FeaturePrefix, StringComparison.Ordinal))
        {
            return Result.Invalid("Only feature flags can be deleted.");
        }

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var row = await db.Set<SystemSetting>().FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
            if (row is null)
            {
                return Result.NotFound("The feature flag was not found.");
            }

            db.Set<SystemSetting>().Remove(row);
            await db.SaveChangesAsync(cancellationToken);
        }

        Invalidate();
        await _audit.WriteAsync(new AuditEntry(AuditCategory, "feature.deleted")
        {
            TargetType = "setting",
            TargetId = key,
            TargetName = key,
            Message = $"Feature flag '{key}' deleted.",
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<SettingChange>>> GetHistoryAsync(string? key = null, int take = 50, CancellationToken cancellationToken = default)
    {
        if (!await _currentUser.HasPermissionAsync(Permissions.Configuration.ManageSettings, cancellationToken))
        {
            return Result<IReadOnlyList<SettingChange>>.Forbidden();
        }

        take = Math.Clamp(take, 1, 200);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<AuditEvent>().AsNoTracking().Where(a => a.Category == AuditCategory);
        if (!string.IsNullOrEmpty(key))
        {
            query = query.Where(a => a.TargetId == key);
        }

        var items = await query
            .OrderByDescending(a => a.OccurredAt)
            .Take(take)
            .Select(a => new SettingChange(a.OccurredAt, a.ActorName, a.Action, a.TargetName ?? a.TargetId, a.Message))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<SettingChange>>.Success(items);
    }

    /// <summary>Removes the cached settings snapshot (for example after a change made outside this service).</summary>
    public void Invalidate() => _cache.Remove(CacheKey);

    private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];

    private async Task<IReadOnlyDictionary<string, StoredSettingValue>> GetStoredAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, StoredSettingValue>? cached) && cached is not null)
        {
            return cached;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Set<SystemSetting>().AsNoTracking()
            .OrderBy(s => s.Key)
            .Take(MaxStoredSettings)
            .Select(s => new StoredSettingValue(s.Key, s.Value ?? string.Empty, s.Category, s.Description, s.UpdatedAt, s.UpdatedBy))
            .ToListAsync(cancellationToken);
        var snapshot = rows.ToDictionary(r => r.Key, StringComparer.Ordinal);
        _cache.Set<IReadOnlyDictionary<string, StoredSettingValue>>(CacheKey, snapshot, s_cacheDuration);
        return snapshot;
    }
}
