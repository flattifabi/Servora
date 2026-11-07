using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Automation.Actions;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Model;
using Servora.Application.Automation.Templates;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Integration.DataSources;
using Servora.Application.Tables;
using Servora.Domain.Integration;

namespace Servora.Application.Integration.Imports;

/// <summary>
/// Executes imports: stages source rows into an <see cref="ImportRun"/> and transforms them into the target table through
/// <see cref="IRecordService"/> (which enforces field validation and the current actor's table permissions). With a
/// coalesce field, rows update the record whose field equals the mapped value and insert otherwise. No permission checks
/// of its own – used by the permission-checked import service and the scheduler.
/// </summary>
/// <param name="dbFactory">The database factory.</param>
/// <param name="records">The record service.</param>
/// <param name="executor">The data source executor.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
public sealed class ImportProcessor(
    IAppDbContextFactory dbFactory,
    IRecordService records,
    DataSourceExecutor executor,
    ICurrentUser currentUser,
    IClock clock,
    ILogger<ImportProcessor> logger)
{
    /// <summary>The maximum number of rows per import run.</summary>
    public const int MaxRows = 5000;

    /// <summary>The maximum accepted CSV upload size in bytes.</summary>
    public const int MaxCsvBytes = 5 * 1024 * 1024;

    private const int StagedDataLength = 8000;

    /// <summary>Runs an import from the configured data source endpoint.</summary>
    /// <param name="set">The import set.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The run identifier.</returns>
    public async Task<Result<Guid>> RunFromDataSourceAsync(ImportSet set, CancellationToken cancellationToken)
    {
        if (set.SourceType != ImportSourceType.DataSource || set.DataSourceId is not { } dataSourceId || string.IsNullOrWhiteSpace(set.EndpointKey))
        {
            return Result<Guid>.Invalid("The import set has no data source endpoint configured.");
        }

        var parameters = FlowJson.TryDeserialize<Dictionary<string, string>>(set.ParametersJson) ?? [];
        var response = await executor.ExecuteDetailedAsync(dataSourceId, set.EndpointKey, parameters, isTest: false, cancellationToken);
        var source = $"{set.EndpointKey} (data source)";
        if (!response.Succeeded)
        {
            return await FailRunAsync(set, source, response.Error ?? "The data source call failed.", cancellationToken);
        }

        if (response.Value!.StatusCode is < 200 or >= 300)
        {
            return await FailRunAsync(set, source, $"The data source returned HTTP {response.Value.StatusCode}.", cancellationToken);
        }

        if (response.Value.Truncated)
        {
            return await FailRunAsync(set, source, "The response exceeded the data source's size limit.", cancellationToken);
        }

        var extracted = ExtractRecords(response.Value.Body, set.RecordsPath);
        return extracted.Succeeded
            ? await RunAsync(set, extracted.Value!, source, cancellationToken)
            : await FailRunAsync(set, source, extracted.Error!, cancellationToken);
    }

    /// <summary>Runs an import from a parsed CSV document.</summary>
    /// <param name="set">The import set.</param>
    /// <param name="document">The CSV document.</param>
    /// <param name="fileName">The uploaded file name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The run identifier.</returns>
    public Task<Result<Guid>> RunFromCsvAsync(ImportSet set, CsvDocument document, string fileName, CancellationToken cancellationToken) =>
        RunAsync(set, RowsFromCsv(document), FlowRedactor.Truncate(fileName, 300)!, cancellationToken);

    /// <summary>Extracts the record array from a JSON response.</summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="path">The path of the array (empty = root).</param>
    /// <returns>The records (non-object items are wrapped as <c>{ "value": … }</c>).</returns>
    public static Result<List<JsonObject>> ExtractRecords(string json, string? path)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 64 });
        }
        catch (JsonException)
        {
            return Result<List<JsonObject>>.Invalid("The response is not valid JSON.");
        }

        var node = string.IsNullOrWhiteSpace(path) ? root : FlowTemplate.Lookup(root, path.Trim());
        if (node is JsonObject single && string.IsNullOrWhiteSpace(path))
        {
            node = new JsonArray(single.DeepClone());
        }

        if (node is not JsonArray array)
        {
            return Result<List<JsonObject>>.Invalid(string.IsNullOrWhiteSpace(path) ? "The response is not a JSON array." : $"The path '{path}' does not point to a JSON array.");
        }

        var rows = new List<JsonObject>();
        foreach (var item in array.Take(MaxRows))
        {
            rows.Add(item is JsonObject obj ? (JsonObject)obj.DeepClone() : new JsonObject { ["value"] = item?.DeepClone() });
        }

        return Result<List<JsonObject>>.Success(rows);
    }

    /// <summary>Converts CSV rows to JSON objects keyed by header.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The rows.</returns>
    public static List<JsonObject> RowsFromCsv(CsvDocument document)
    {
        var rows = new List<JsonObject>();
        foreach (var row in document.Rows.Take(MaxRows))
        {
            var obj = new JsonObject();
            for (var c = 0; c < document.Headers.Count; c++)
            {
                obj[document.Headers[c]] = row[c];
            }

            rows.Add(obj);
        }

        return rows;
    }

    /// <summary>Applies a transform map to one source row.</summary>
    /// <param name="row">The source row.</param>
    /// <param name="maps">The field maps.</param>
    /// <returns>The target values keyed by field (empty values omitted).</returns>
    public static Dictionary<string, object?> Transform(JsonObject row, IReadOnlyList<ImportFieldMap> maps)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var map in maps)
        {
            if (string.IsNullOrWhiteSpace(map.TargetField) || string.IsNullOrWhiteSpace(map.SourcePath))
            {
                continue;
            }

            var node = Resolve(row, map.SourcePath.Trim());
            object? value = node is JsonObject or JsonArray ? FlowValues.ToRecordValue(node) : FlowTemplate.ToText(node);
            if (value is string text)
            {
                text = map.Transform?.ToLowerInvariant() switch
                {
                    "none" => text,
                    "upper" => text.Trim().ToUpperInvariant(),
                    "lower" => text.Trim().ToLowerInvariant(),
                    _ => text.Trim(),
                };
                value = text.Length == 0 ? null : text;
            }

            value ??= string.IsNullOrEmpty(map.DefaultValue) ? null : map.DefaultValue;
            if (value is not null)
            {
                values[map.TargetField.Trim()] = value;
            }
        }

        return values;
    }

    /// <summary>
    /// Resolves a source path: an exact column / property name first (CSV headers may contain spaces or dots), then a
    /// dotted JSON path (<c>address.city</c>, <c>items[0].id</c>).
    /// </summary>
    /// <param name="row">The source row.</param>
    /// <param name="sourcePath">The column name or path.</param>
    /// <returns>The node or <see langword="null"/>.</returns>
    private static JsonNode? Resolve(JsonObject row, string sourcePath)
    {
        if (row.TryGetPropertyValue(sourcePath, out var exact))
        {
            return exact;
        }

        foreach (var pair in row)
        {
            if (string.Equals(pair.Key.Trim(), sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return FlowTemplate.Lookup(row, sourcePath);
    }

    private async Task<Result<Guid>> RunAsync(ImportSet set, IReadOnlyList<JsonObject> sourceRows, string sourceDescription, CancellationToken cancellationToken)
    {
        var run = new ImportRun
        {
            ImportSetId = set.Id,
            Status = ImportRunStatus.Running,
            SourceDescription = sourceDescription,
            StartedAt = clock.UtcNow,
            StartedBy = FlowRedactor.Truncate(currentUser.DisplayName, 256),
            TotalRows = Math.Min(sourceRows.Count, MaxRows),
        };

        // 1. Stage the rows.
        var staged = new List<(ImportRow Row, JsonObject Data)>();
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.Set<ImportRun>().Add(run);
            var number = 0;
            foreach (var data in sourceRows.Take(MaxRows))
            {
                var row = new ImportRow
                {
                    RunId = run.Id,
                    RowNumber = ++number,
                    Status = ImportRowStatus.Pending,
                    DataJson = Stage(data),
                };
                db.Set<ImportRow>().Add(row);
                staged.Add((row, data));
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        // 2. Transform into the target table.
        var maps = FlowJson.TryDeserialize<List<ImportFieldMap>>(set.FieldMapsJson) ?? [];
        var coalesceMap = string.IsNullOrWhiteSpace(set.CoalesceField) ? null : maps.FirstOrDefault(m => string.Equals(m.TargetField, set.CoalesceField, StringComparison.OrdinalIgnoreCase));
        foreach (var (row, data) in staged)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await TransformRowAsync(set, row, data, maps, coalesceMap, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Import {ImportSet} row {Row} failed.", set.Name, row.RowNumber);
                row.Status = ImportRowStatus.Error;
                row.Message = "Unexpected error while writing the row.";
            }
        }

        // 3. Persist row results and counters.
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            foreach (var chunk in staged.Select(s => s.Row).Chunk(500))
            {
                var ids = chunk.Select(r => r.Id).ToHashSet();
                var first = chunk[0].RowNumber;
                var last = chunk[^1].RowNumber;
                var tracked = await db.Set<ImportRow>().Where(r => r.RunId == run.Id && r.RowNumber >= first && r.RowNumber <= last).ToListAsync(cancellationToken);
                foreach (var entity in tracked.Where(t => ids.Contains(t.Id)))
                {
                    var source = chunk.First(r => r.Id == entity.Id);
                    entity.Status = source.Status;
                    entity.TargetRecordId = source.TargetRecordId;
                    entity.Message = FlowRedactor.Truncate(source.Message, 1000);
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            var storedRun = await db.Set<ImportRun>().FirstAsync(r => r.Id == run.Id, cancellationToken);
            storedRun.Inserted = staged.Count(s => s.Row.Status == ImportRowStatus.Inserted);
            storedRun.Updated = staged.Count(s => s.Row.Status == ImportRowStatus.Updated);
            storedRun.Skipped = staged.Count(s => s.Row.Status == ImportRowStatus.Skipped);
            storedRun.Errors = staged.Count(s => s.Row.Status == ImportRowStatus.Error);
            storedRun.Status = storedRun.Errors > 0 ? ImportRunStatus.CompletedWithErrors : ImportRunStatus.Completed;
            storedRun.CompletedAt = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await TouchLastRunAsync(db, set.Id, run.StartedAt, cancellationToken);
        }

        return Result<Guid>.Success(run.Id);
    }

    private async Task TransformRowAsync(ImportSet set, ImportRow row, JsonObject data, IReadOnlyList<ImportFieldMap> maps, ImportFieldMap? coalesceMap, CancellationToken cancellationToken)
    {
        var values = Transform(data, maps);
        if (values.Count == 0)
        {
            row.Status = ImportRowStatus.Skipped;
            row.Message = "No mapped values.";
            return;
        }

        if (coalesceMap is not null)
        {
            if (!values.TryGetValue(coalesceMap.TargetField.Trim(), out var key) || key is null)
            {
                row.Status = ImportRowStatus.Error;
                row.Message = $"The coalesce field '{set.CoalesceField}' has no value.";
                return;
            }

            var condition = new ConditionGroup { Rules = [new ConditionRule { Field = coalesceMap.TargetField.Trim(), Operator = ConditionOperator.Equals, Value = Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture) }] };
            var existing = await records.QueryAsync(set.TargetTable, new RecordQuery { Condition = condition, Page = new PageRequest { PageSize = 2 }, Fields = ["Id"], IncludeDisplayValues = false }, cancellationToken);
            if (!existing.Succeeded)
            {
                row.Status = ImportRowStatus.Error;
                row.Message = existing.Error;
                return;
            }

            if (existing.Value!.Items.Count > 1)
            {
                row.Status = ImportRowStatus.Error;
                row.Message = $"More than one record matches {set.CoalesceField} = {key}.";
                return;
            }

            if (existing.Value.Items.Count == 1 && existing.Value.Items[0].TryGetValue("Id", out var idValue) && Guid.TryParse(Convert.ToString(idValue, System.Globalization.CultureInfo.InvariantCulture), out var existingId))
            {
                var updated = await records.UpdateAsync(set.TargetTable, existingId, values, cancellationToken);
                row.Status = updated.Succeeded ? ImportRowStatus.Updated : ImportRowStatus.Error;
                row.TargetRecordId = existingId;
                row.Message = updated.Succeeded ? null : updated.Error;
                return;
            }
        }

        var created = await records.CreateAsync(set.TargetTable, values, cancellationToken);
        row.Status = created.Succeeded ? ImportRowStatus.Inserted : ImportRowStatus.Error;
        row.TargetRecordId = created.Succeeded ? created.Value : null;
        row.Message = created.Succeeded ? null : created.Error;
    }

    private async Task<Result<Guid>> FailRunAsync(ImportSet set, string source, string error, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.UtcNow;
        var run = new ImportRun
        {
            ImportSetId = set.Id,
            Status = ImportRunStatus.Failed,
            SourceDescription = source,
            StartedAt = now,
            CompletedAt = now,
            StartedBy = FlowRedactor.Truncate(currentUser.DisplayName, 256),
            Error = FlowRedactor.Truncate(error, 2000),
        };
        db.Set<ImportRun>().Add(run);
        await db.SaveChangesAsync(cancellationToken);
        await TouchLastRunAsync(db, set.Id, now, cancellationToken);
        return Result<Guid>.Failure(error);
    }

    private static Task TouchLastRunAsync(IAppDbContext db, Guid importSetId, DateTimeOffset startedAt, CancellationToken cancellationToken) =>
        db.Set<ImportSet>().Where(s => s.Id == importSetId).ExecuteUpdateAsync(setters => setters.SetProperty(s => s.LastRunAt, (DateTimeOffset?)startedAt), cancellationToken);

    private static string Stage(JsonObject data)
    {
        var json = FlowRedactor.Redact(data)!.ToJsonString();
        return json.Length <= StagedDataLength ? json : new JsonObject { ["_truncated"] = true, ["preview"] = json[..(StagedDataLength - 100)] }.ToJsonString();
    }
}
