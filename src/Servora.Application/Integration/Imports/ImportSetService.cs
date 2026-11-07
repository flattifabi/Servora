using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Model;
using Servora.Application.Automation.Scheduling;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.Integration;

namespace Servora.Application.Integration.Imports;

/// <summary>Default <see cref="IImportSetService"/>.</summary>
/// <param name="dbFactory">The database factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="processor">The import processor.</param>
/// <param name="tables">The table registry.</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
public sealed class ImportSetService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    ImportProcessor processor,
    ITableRegistry tables,
    IAuditService audit,
    IClock clock) : IImportSetService
{
    /// <inheritdoc />
    public async Task<Result<PagedResult<ImportSetListItem>>> ListAsync(PageRequest page, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<PagedResult<ImportSetListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var sets = db.Set<ImportSet>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var search = page.Search.Trim();
            sets = sets.Where(s => s.Name.Contains(search) || s.TargetTable.Contains(search));
        }

        sets = page.SortBy switch
        {
            "TargetTable" => page.SortDescending ? sets.OrderByDescending(s => s.TargetTable) : sets.OrderBy(s => s.TargetTable),
            "LastRunAt" => page.SortDescending ? sets.OrderByDescending(s => s.LastRunAt) : sets.OrderBy(s => s.LastRunAt),
            _ => page.SortDescending ? sets.OrderByDescending(s => s.Name) : sets.OrderBy(s => s.Name),
        };

        var result = await sets
            .Select(s => new ImportSetListItem(s.Id, s.Name, s.SourceType, s.TargetTable, s.CoalesceField, s.ScheduleEnabled, s.NextRunAt, s.LastRunAt))
            .ToPagedResultAsync(page, cancellationToken);
        var items = new List<ImportSetListItem>(result.Items.Count);
        foreach (var item in result.Items)
        {
            var last = await db.Set<ImportRun>().AsNoTracking().Where(r => r.ImportSetId == item.Id).OrderByDescending(r => r.StartedAt)
                .Select(r => new { r.Status, r.TotalRows }).FirstOrDefaultAsync(cancellationToken);
            items.Add(item with { LastRunStatus = last?.Status, LastRunRows = last?.TotalRows ?? 0 });
        }

        return Result<PagedResult<ImportSetListItem>>.Success(result with { Items = items });
    }

    /// <inheritdoc />
    public async Task<Result<ImportOverview>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<ImportOverview>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var since = clock.UtcNow.AddDays(-7);
        var runs = await db.Set<ImportRun>().AsNoTracking().Where(r => r.StartedAt >= since).OrderByDescending(r => r.StartedAt).Take(5000)
            .Select(r => new { r.Inserted, r.Updated, r.Errors }).ToListAsync(cancellationToken);
        return Result<ImportOverview>.Success(new ImportOverview(
            await db.Set<ImportSet>().CountAsync(cancellationToken),
            await db.Set<ImportSet>().CountAsync(s => s.ScheduleEnabled, cancellationToken),
            runs.Count,
            runs.Sum(r => r.Inserted + r.Updated),
            runs.Sum(r => r.Errors)));
    }

    /// <inheritdoc />
    public async Task<Result<ImportSetModel>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<ImportSetModel>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var set = await db.Set<ImportSet>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return set is null ? Result<ImportSetModel>.NotFound("The import set was not found.") : Result<ImportSetModel>.Success(ToModel(set));
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveAsync(ImportSetModel model, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        var validation = await ValidateAsync(model, cancellationToken);
        if (validation is not null)
        {
            return Result<Guid>.Invalid(validation);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var name = model.Name.Trim();
        if (await db.Set<ImportSet>().AnyAsync(s => s.Name == name && s.Id != model.Id, cancellationToken))
        {
            return Result<Guid>.Conflict($"An import set named '{name}' already exists.");
        }

        ImportSet? set;
        var created = model.Id == Guid.Empty;
        if (created)
        {
            set = new ImportSet();
            db.Set<ImportSet>().Add(set);
        }
        else
        {
            set = await db.Set<ImportSet>().FirstOrDefaultAsync(s => s.Id == model.Id, cancellationToken);
            if (set is null)
            {
                return Result<Guid>.NotFound("The import set was not found.");
            }

            if (set.Version != model.ConcurrencyVersion)
            {
                return Result<Guid>.Conflict("The import set was changed by someone else. Reload it and try again.");
            }
        }

        set.Name = name;
        set.Description = FlowRedactor.Truncate(model.Description?.Trim(), 2000);
        set.SourceType = model.SourceType;
        set.DataSourceId = model.SourceType == ImportSourceType.DataSource ? model.DataSourceId : null;
        set.EndpointKey = model.SourceType == ImportSourceType.DataSource ? model.EndpointKey?.Trim() : null;
        set.ParametersJson = model.SourceType == ImportSourceType.DataSource && model.Parameters.Count > 0 ? JsonSerializer.Serialize(model.Parameters) : null;
        set.RecordsPath = string.IsNullOrWhiteSpace(model.RecordsPath) ? null : model.RecordsPath.Trim();
        set.TargetTable = model.TargetTable.Trim();
        set.CoalesceField = string.IsNullOrWhiteSpace(model.CoalesceField) ? null : model.CoalesceField.Trim();
        set.FieldMapsJson = FlowJson.Serialize(model.FieldMaps.Where(m => !string.IsNullOrWhiteSpace(m.SourcePath) && !string.IsNullOrWhiteSpace(m.TargetField)).ToList());
        set.CronExpression = string.IsNullOrWhiteSpace(model.CronExpression) ? null : model.CronExpression.Trim();
        set.TimeZoneId = string.IsNullOrWhiteSpace(model.TimeZoneId) ? null : model.TimeZoneId.Trim();
        set.ScheduleEnabled = model.ScheduleEnabled && model.SourceType == ImportSourceType.DataSource && set.CronExpression is not null;
        set.NextRunAt = ComputeNextRun(set, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("integration", created ? "import_set.created" : "import_set.updated") { TargetType = "import_set", TargetId = set.Id.ToString(), TargetName = set.Name }, cancellationToken);
        return Result<Guid>.Success(set.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var set = await db.Set<ImportSet>().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (set is null)
        {
            return Result.NotFound("The import set was not found.");
        }

        db.Set<ImportSet>().Remove(set);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("integration", "import_set.deleted") { TargetType = "import_set", TargetId = id.ToString(), TargetName = set.Name }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> RunNowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        var set = await LoadAsync(id, cancellationToken);
        if (set is null)
        {
            return Result<Guid>.NotFound("The import set was not found.");
        }

        var result = await processor.RunFromDataSourceAsync(set, cancellationToken);
        await audit.WriteAsync(new AuditEntry("integration", "import_set.run") { TargetType = "import_set", TargetId = id.ToString(), TargetName = set.Name, Message = result.Succeeded ? $"Run {result.Value}" : result.Error }, cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> RunCsvAsync(Guid id, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        var set = await LoadAsync(id, cancellationToken);
        if (set is null)
        {
            return Result<Guid>.NotFound("The import set was not found.");
        }

        var text = await ReadBoundedAsync(content, cancellationToken);
        if (text is null)
        {
            return Result<Guid>.Invalid($"The file exceeds {ImportProcessor.MaxCsvBytes / 1024 / 1024} MB.");
        }

        var document = CsvParser.Parse(text, ImportProcessor.MaxRows);
        if (!document.Succeeded)
        {
            return Result<Guid>.From(document);
        }

        var result = await processor.RunFromCsvAsync(set, document.Value!, Path.GetFileName(fileName), cancellationToken);
        await audit.WriteAsync(new AuditEntry("integration", "import_set.run") { TargetType = "import_set", TargetId = id.ToString(), TargetName = set.Name, Message = $"CSV upload {Path.GetFileName(fileName)}: {document.Value!.Rows.Count} rows" }, cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result<CsvDocument>> PreviewCsvAsync(Stream content, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<CsvDocument>.Forbidden();
        }

        var text = await ReadBoundedAsync(content, cancellationToken);
        return text is null ? Result<CsvDocument>.Invalid("The file is too large.") : CsvParser.Parse(text, 5);
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<ImportRunItem>>> ListRunsAsync(Guid id, PageRequest page, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<PagedResult<ImportRunItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var runs = await db.Set<ImportRun>().AsNoTracking()
            .Where(r => r.ImportSetId == id)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => new ImportRunItem(r.Id, r.ImportSetId, r.Status, r.SourceDescription, r.StartedAt, r.CompletedAt, r.TotalRows, r.Inserted, r.Updated, r.Skipped, r.Errors, r.StartedBy, r.Error))
            .ToPagedResultAsync(page, cancellationToken);
        return Result<PagedResult<ImportRunItem>>.Success(runs);
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<ImportRowItem>>> ListRowsAsync(Guid runId, ImportRowStatus? status, PageRequest page, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<PagedResult<ImportRowItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = db.Set<ImportRow>().AsNoTracking().Where(r => r.RunId == runId);
        if (status is { } filter)
        {
            rows = rows.Where(r => r.Status == filter);
        }

        var result = await rows.OrderBy(r => r.RowNumber)
            .Select(r => new ImportRowItem(r.Id, r.RowNumber, r.Status, r.DataJson, r.TargetRecordId, r.Message))
            .ToPagedResultAsync(page, cancellationToken);
        return Result<PagedResult<ImportRowItem>>.Success(result);
    }

    /// <summary>Computes the next scheduled run of an import set.</summary>
    /// <param name="set">The import set.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The next run or <see langword="null"/>.</returns>
    public static DateTimeOffset? ComputeNextRun(ImportSet set, DateTimeOffset now)
    {
        if (!set.ScheduleEnabled || !CronExpression.TryParse(set.CronExpression, out var cron, out _))
        {
            return null;
        }

        CronExpression.TryFindTimeZone(set.TimeZoneId, out var zone);
        return cron!.GetNextOccurrence(now, zone);
    }

    private static async Task<string?> ReadBoundedAsync(Stream content, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81_920];
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + read > ImportProcessor.MaxCsvBytes)
            {
                return null;
            }

            memory.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length);
    }

    private static ImportSetModel ToModel(ImportSet set) => new()
    {
        Id = set.Id,
        Name = set.Name,
        Description = set.Description,
        SourceType = set.SourceType,
        DataSourceId = set.DataSourceId,
        EndpointKey = set.EndpointKey,
        Parameters = FlowJson.TryDeserialize<Dictionary<string, string>>(set.ParametersJson) ?? [],
        RecordsPath = set.RecordsPath,
        TargetTable = set.TargetTable,
        CoalesceField = set.CoalesceField,
        FieldMaps = FlowJson.TryDeserialize<List<ImportFieldMap>>(set.FieldMapsJson) ?? [],
        CronExpression = set.CronExpression,
        TimeZoneId = set.TimeZoneId,
        ScheduleEnabled = set.ScheduleEnabled,
        NextRunAt = set.NextRunAt,
        LastRunAt = set.LastRunAt,
        ConcurrencyVersion = set.Version,
    };

    private async Task<string?> ValidateAsync(ImportSetModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Name) || model.Name.Trim().Length > 200)
        {
            return "Enter a name of up to 200 characters.";
        }

        var table = string.IsNullOrWhiteSpace(model.TargetTable) ? null : await tables.GetTableAsync(model.TargetTable.Trim(), cancellationToken);
        if (table is null)
        {
            return "Choose an existing target table.";
        }

        if (model.FieldMaps.Count > 200)
        {
            return "A transform map can have at most 200 field maps.";
        }

        foreach (var map in model.FieldMaps.Where(m => !string.IsNullOrWhiteSpace(m.TargetField)))
        {
            var field = table.FindField(map.TargetField.Trim());
            if (field is null)
            {
                return $"The table '{table.Label}' has no field '{map.TargetField}'.";
            }

            if (field.IsReadOnly)
            {
                return $"The field '{field.Label}' is read-only and cannot be imported.";
            }

            if (map.SourcePath.Length > 300)
            {
                return "A source path is too long.";
            }
        }

        if (!string.IsNullOrWhiteSpace(model.CoalesceField) && !model.FieldMaps.Any(m => string.Equals(m.TargetField?.Trim(), model.CoalesceField.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return "The coalesce field must be one of the mapped target fields.";
        }

        if (model.SourceType == ImportSourceType.DataSource && (model.DataSourceId is null || string.IsNullOrWhiteSpace(model.EndpointKey)))
        {
            return "Choose the data source endpoint that returns the records.";
        }

        if (!string.IsNullOrWhiteSpace(model.CronExpression) && !CronExpression.TryParse(model.CronExpression, out _, out var cronError))
        {
            return cronError;
        }

        if (!CronExpression.TryFindTimeZone(model.TimeZoneId, out _))
        {
            return $"The time zone '{model.TimeZoneId}' is unknown.";
        }

        return null;
    }

    private async Task<ImportSet?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<ImportSet>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    private Task<bool> CanManageAsync(CancellationToken cancellationToken) =>
        currentUser.HasPermissionAsync(Permissions.Integration.ManageImports, cancellationToken);
}
