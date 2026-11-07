using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Configuration;

namespace Servora.Application.Observability;

/// <summary>Reads and writes the module's own system settings (<see cref="SystemSetting"/> rows).</summary>
internal static class SystemSettingAccess
{
    /// <summary>Reads a setting value.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The value or <see langword="null"/>.</returns>
    public static Task<string?> GetAsync(IAppDbContext db, string key, CancellationToken cancellationToken) =>
        db.Set<SystemSetting>().AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);

    /// <summary>Reads an integer setting.</summary>
    /// <param name="db">The database context.</param>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The value used when the setting is missing or invalid.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The value.</returns>
    public static async Task<int> GetIntAsync(IAppDbContext db, string key, int fallback, CancellationToken cancellationToken) =>
        int.TryParse(await GetAsync(db, key, cancellationToken), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    /// <summary>Creates or updates a setting (tracked; the caller saves).</summary>
    /// <param name="db">The database context.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <param name="description">The description shown in the settings UI.</param>
    /// <param name="valueType">The value type hint.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the entity is tracked.</returns>
    public static async Task SetAsync(IAppDbContext db, string key, string? value, string description, string valueType, CancellationToken cancellationToken)
    {
        var setting = await db.Set<SystemSetting>().FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null)
        {
            db.Set<SystemSetting>().Add(new SystemSetting
            {
                Key = key,
                Value = value,
                Category = ObservabilitySettingKeys.Category,
                Description = description,
                ValueType = valueType,
            });
        }
        else
        {
            setting.Value = value;
        }
    }
}
