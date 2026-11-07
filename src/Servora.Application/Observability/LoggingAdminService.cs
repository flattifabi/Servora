using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Application.Observability;

/// <summary>Default <see cref="ILoggingAdminService"/>.</summary>
/// <param name="currentUser">The current actor.</param>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="levels">The runtime level controller.</param>
/// <param name="store">The in-memory log buffer.</param>
/// <param name="files">The log file store.</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
public sealed partial class LoggingAdminService(
    ICurrentUser currentUser,
    IAppDbContextFactory dbFactory,
    ILogLevelController levels,
    ILogStore store,
    ILogFileStore files,
    IAuditService audit,
    IClock clock,
    ILogger<LoggingAdminService> logger) : ILoggingAdminService
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Serializes runtime level settings for persistence.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The JSON.</returns>
    public static string SerializeLevels(LogLevelSettings settings) => JsonSerializer.Serialize(settings, s_json);

    /// <summary>Deserializes persisted runtime level settings; invalid JSON yields the defaults.</summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The settings.</returns>
    public static LogLevelSettings DeserializeLevels(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return LogLevelSettings.Defaults;
        }

        try
        {
            return JsonSerializer.Deserialize<LogLevelSettings>(json, s_json) ?? LogLevelSettings.Defaults;
        }
        catch (JsonException)
        {
            return LogLevelSettings.Defaults;
        }
    }

    /// <summary>Builds the settings that result from an update (including the auto-revert snapshot).</summary>
    /// <param name="current">The active settings.</param>
    /// <param name="update">The requested change.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The new settings.</returns>
    public static LogLevelSettings Compose(LogLevelSettings current, LogLevelUpdate update, DateTimeOffset now)
    {
        // The baseline is what was active before any pending auto-revert session started.
        var baseline = current.RevertAt is null ? current with { RevertAt = null, RevertTo = null } : current.RevertTo ?? LogLevelSettings.Defaults;
        return new LogLevelSettings
        {
            GlobalLevel = update.GlobalLevel,
            Overrides = update.Overrides,
            RevertAt = update.AutoRevertMinutes is { } minutes ? now.AddMinutes(minutes) : null,
            RevertTo = update.AutoRevertMinutes is null ? null : baseline,
        };
    }

    /// <summary>Validates a level update.</summary>
    /// <param name="update">The update.</param>
    /// <returns>The error message, or <see langword="null"/> when valid.</returns>
    public static string? Validate(LogLevelUpdate update)
    {
        if (update.AutoRevertMinutes is < 1 or > 1440)
        {
            return "Auto-revert must be between 1 and 1440 minutes.";
        }

        if (update.Overrides.Count > 50)
        {
            return "At most 50 category overrides are allowed.";
        }

        foreach (var categoryOverride in update.Overrides)
        {
            if (string.IsNullOrWhiteSpace(categoryOverride.Prefix) || categoryOverride.Prefix.Length > 200 || !CategoryPattern().IsMatch(categoryOverride.Prefix.Trim()))
            {
                return $"'{categoryOverride.Prefix}' is not a valid category prefix (letters, digits, '.', '_', '-' and '*').";
            }

            if (!Enum.IsDefined(categoryOverride.Level))
            {
                return "Invalid log level.";
            }
        }

        return update.GlobalLevel is { } level && (!Enum.IsDefined(level) || level == LogLevel.None) ? "Invalid global log level." : null;
    }

    /// <inheritdoc />
    public async Task<Result<LoggingOverview>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadLogs, cancellationToken))
        {
            return Result<LoggingOverview>.Forbidden();
        }

        return Result<LoggingOverview>.Success(new LoggingOverview(
            levels.Current,
            levels.ConfiguredDefaultLevel,
            store.GetStatistics(),
            files.Retention,
            files.Directory,
            files.ListFiles(),
            store.GetCategories()));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<LogEntry>>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadLogs, cancellationToken))
        {
            return Result<IReadOnlyList<LogEntry>>.Forbidden();
        }

        return Result<IReadOnlyList<LogEntry>>.Success(store.Query(query));
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ExportAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadLogs, cancellationToken))
        {
            return Result<byte[]>.Forbidden();
        }

        var builder = new StringBuilder();
        foreach (var entry in store.Query(query).Reverse())
        {
            builder.Append(JsonSerializer.Serialize(entry, s_json)).Append('\n');
        }

        await audit.WriteAsync(new AuditEntry("observability", "logs.exported") { Message = "Filtered log entries were exported." }, cancellationToken);
        return Result<byte[]>.Success(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    /// <inheritdoc />
    public async Task<Result<Stream>> OpenFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadLogs, cancellationToken))
        {
            return Result<Stream>.Forbidden();
        }

        var stream = files.OpenRead(fileName);
        if (stream is null)
        {
            return Result<Stream>.NotFound("The log file was not found.");
        }

        await audit.WriteAsync(new AuditEntry("observability", "logs.file_downloaded") { TargetType = "log_file", TargetName = fileName }, cancellationToken);
        return Result<Stream>.Success(stream);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateLevelsAsync(LogLevelUpdate update, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ManageLogs, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (Validate(update) is { } error)
        {
            return Result.Invalid(error);
        }

        var settings = Compose(levels.Current, update, clock.UtcNow);
        await PersistAndApplyAsync(settings, cancellationToken);
        await audit.WriteAsync(new AuditEntry("config", "logging.levels.changed")
        {
            TargetType = "logging",
            Message = DescribeLevels(settings),
            Details = new
            {
                global = settings.GlobalLevel?.ToString() ?? "default",
                overrides = settings.Overrides.Select(o => $"{o.Prefix}={o.Level}").ToList(),
                revertAt = settings.RevertAt,
            },
        }, cancellationToken);
        logger.LogWarning("Runtime log levels changed by {Actor}: {Levels}", currentUser.DisplayName, DescribeLevels(settings));
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ResetLevelsAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ManageLogs, cancellationToken))
        {
            return Result.Forbidden();
        }

        await PersistAndApplyAsync(LogLevelSettings.Defaults, cancellationToken);
        await audit.WriteAsync(new AuditEntry("config", "logging.levels.reset") { TargetType = "logging", Message = "Runtime log levels reset to configuration defaults." }, cancellationToken);
        logger.LogInformation("Runtime log levels reset to configuration defaults by {Actor}.", currentUser.DisplayName);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> UpdateRetentionAsync(LogRetentionSettings settings, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ManageLogs, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (settings.Validate() is { } error)
        {
            return Result.Invalid(error);
        }

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            await SystemSettingAccess.SetAsync(db, ObservabilitySettingKeys.LogRetentionDays, settings.RetentionDays.ToString(CultureInfo.InvariantCulture), "Days log files are kept.", "int", cancellationToken);
            await SystemSettingAccess.SetAsync(db, ObservabilitySettingKeys.LogMaxFileSizeMb, settings.MaxFileSizeMb.ToString(CultureInfo.InvariantCulture), "Maximum size of a log file before it rolls over (MB).", "int", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        files.Configure(settings);
        var deleted = files.ApplyRetention();
        await audit.WriteAsync(new AuditEntry("config", "logging.retention.changed")
        {
            TargetType = "logging",
            Message = $"Log retention set to {settings.RetentionDays} days, max file size {settings.MaxFileSizeMb} MB ({deleted} expired file(s) deleted).",
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task RestorePersistedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = DeserializeLevels(await SystemSettingAccess.GetAsync(db, ObservabilitySettingKeys.LogLevels, cancellationToken));
        levels.Apply(settings);

        var retention = new LogRetentionSettings(
            await SystemSettingAccess.GetIntAsync(db, ObservabilitySettingKeys.LogRetentionDays, files.Retention.RetentionDays, cancellationToken),
            await SystemSettingAccess.GetIntAsync(db, ObservabilitySettingKeys.LogMaxFileSizeMb, files.Retention.MaxFileSizeMb, cancellationToken));
        files.Configure(retention);

        if (settings.HasOverrides)
        {
            logger.LogInformation("Restored runtime log levels: {Levels}", DescribeLevels(settings));
        }
    }

    /// <inheritdoc />
    public async Task<bool> RevertIfDueAsync(CancellationToken cancellationToken = default)
    {
        var current = levels.Current;
        if (current.RevertAt is not { } revertAt || revertAt > clock.UtcNow)
        {
            return false;
        }

        var target = current.RevertTo ?? LogLevelSettings.Defaults;
        await PersistAndApplyAsync(target, cancellationToken);
        await audit.WriteAsync(new AuditEntry("config", "logging.levels.reverted") { TargetType = "logging", Message = "Debug logging automatically reverted: " + DescribeLevels(target) }, cancellationToken);
        logger.LogInformation("Runtime log levels automatically reverted: {Levels}", DescribeLevels(target));
        return true;
    }

    /// <summary>Describes settings in one line for audit and log messages.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The description.</returns>
    public static string DescribeLevels(LogLevelSettings settings)
    {
        var text = "global " + (settings.GlobalLevel?.ToString() ?? "default");
        if (settings.Overrides.Count > 0)
        {
            text += "; " + string.Join(", ", settings.Overrides.Select(o => $"{o.Prefix}={o.Level}"));
        }

        if (settings.RevertAt is { } revertAt)
        {
            text += $"; reverts at {revertAt:u}";
        }

        return text;
    }

    private async Task PersistAndApplyAsync(LogLevelSettings settings, CancellationToken cancellationToken)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            await SystemSettingAccess.SetAsync(db, ObservabilitySettingKeys.LogLevels, SerializeLevels(settings), "Runtime log levels (debug switch).", "json", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        levels.Apply(settings);
    }

    [GeneratedRegex(@"^[A-Za-z0-9_.*\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CategoryPattern();
}
