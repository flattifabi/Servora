using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Events;
using Servora.Application.Observability;
using Servora.Application.Platform;
using Servora.Domain.Common;
using Servora.Domain.Platform;

namespace Servora.Application.Tables;

/// <summary>
/// Generic, permission-checked CRUD over typed tables and custom tables. Only fields of the registry's table definition
/// are readable, filterable, sortable and writable, so <see cref="NotExposedAttribute"/> properties can never be
/// accessed. Values are converted and validated per field type; references must point to existing records.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="registry">The table registry.</param>
/// <param name="access">The table access service.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="numbers">The number sequence service.</param>
/// <param name="audit">The audit service.</param>
/// <param name="logger">The logger.</param>
public sealed class RecordService(
    IAppDbContextFactory dbFactory,
    ITableRegistry registry,
    ITableAccessService access,
    ICurrentUser currentUser,
    INumberSequenceService numbers,
    IAuditService audit,
    ILogger<RecordService> logger) : IRecordService
{
    /// <summary>The maximum length of <see cref="IHasCustomFields.CustomFieldsJson"/> written by the service.</summary>
    public const int MaxCustomFieldsJsonLength = 32_000;

    /// <summary>The maximum length of <see cref="CustomRecord.DataJson"/>.</summary>
    public const int MaxDataJsonLength = 64_000;

    /// <summary>The maximum number of field values accepted per write.</summary>
    public const int MaxValuesPerWrite = 200;

    /// <summary>The maximum length of a free-text search term.</summary>
    public const int MaxSearchLength = 200;

    private const string DisplaySuffix = ".display";
    private const int MaxReferenceIds = 1000;

    /// <inheritdoc />
    public async Task<Result<PagedResult<IReadOnlyDictionary<string, object?>>>> QueryAsync(string tableName, RecordQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var table = await registry.GetTableAsync(tableName, cancellationToken);
        if (table is null)
        {
            return Result<PagedResult<IReadOnlyDictionary<string, object?>>>.NotFound($"Table '{tableName}' does not exist.");
        }

        if (!await access.IsAllowedAsync(table.Name, TableOperation.Read, cancellationToken))
        {
            return Result<PagedResult<IReadOnlyDictionary<string, object?>>>.Forbidden();
        }

        if (ConditionValidator.Validate(table, query.Condition) is { } conditionError)
        {
            return Result<PagedResult<IReadOnlyDictionary<string, object?>>>.Invalid(conditionError);
        }

        if (ValidateFieldList(table, query.Fields) is { } fieldsError)
        {
            return Result<PagedResult<IReadOnlyDictionary<string, object?>>>.Invalid(fieldsError);
        }

        string? sortBy = null;
        if (!string.IsNullOrWhiteSpace(query.Page.SortBy))
        {
            sortBy = table.FindField(query.Page.SortBy)?.Name;
            if (sortBy is null)
            {
                return Result<PagedResult<IReadOnlyDictionary<string, object?>>>.Invalid($"Unknown sort field '{query.Page.SortBy}'.");
            }
        }

        if (query.Page.Search is { Length: > MaxSearchLength })
        {
            return Result<PagedResult<IReadOnlyDictionary<string, object?>>>.Invalid($"The search term must not exceed {MaxSearchLength} characters.");
        }

        var effective = query with { Page = query.Page with { SortBy = sortBy } };
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var accessor = TableAccessors.For(table);
        var (items, total) = await accessor.QueryAsync(db, table, effective, new ConditionContext(currentUser.UserId), cancellationToken);

        var fields = SelectFields(table, query.Fields);
        var rows = items.Select(e => Project(RecordEntityAccessor.ReadValues(table, e), fields)).ToList();
        if (query.IncludeDisplayValues)
        {
            await AddDisplayValuesAsync(db, fields, rows, cancellationToken);
        }

        return Result<PagedResult<IReadOnlyDictionary<string, object?>>>.Success(
            new PagedResult<IReadOnlyDictionary<string, object?>>(rows, total, query.Page.Page, query.Page.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyDictionary<string, object?>>> GetAsync(string tableName, Guid id, CancellationToken cancellationToken = default)
    {
        var table = await registry.GetTableAsync(tableName, cancellationToken);
        if (table is null)
        {
            return Result<IReadOnlyDictionary<string, object?>>.NotFound($"Table '{tableName}' does not exist.");
        }

        if (!await access.IsAllowedAsync(table.Name, TableOperation.Read, cancellationToken))
        {
            return Result<IReadOnlyDictionary<string, object?>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await TableAccessors.For(table).FindAsync(db, table, id, tracking: false, cancellationToken);
        if (entity is null)
        {
            return Result<IReadOnlyDictionary<string, object?>>.NotFound();
        }

        var row = Project(RecordEntityAccessor.ReadValues(table, entity), table.Fields);
        await AddDisplayValuesAsync(db, table.Fields, [row], cancellationToken);
        return Result<IReadOnlyDictionary<string, object?>>.Success(row);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(string tableName, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var table = await registry.GetTableAsync(tableName, cancellationToken);
        if (table is null)
        {
            return Result<Guid>.NotFound($"Table '{tableName}' does not exist.");
        }

        if (!await access.IsAllowedAsync(table.Name, TableOperation.Create, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        string? numberPrefix = TablePolicies.Find(table.Name)?.NumberPrefix;
        if (table.IsCustom)
        {
            var meta = await db.Set<CustomTable>().AsNoTracking().FirstOrDefaultAsync(t => t.Name == table.Name, cancellationToken);
            if (meta is null)
            {
                return Result<Guid>.NotFound($"Table '{tableName}' does not exist.");
            }

            if (!meta.IsActive)
            {
                return Result<Guid>.Invalid($"The table '{table.Label}' is inactive and does not accept new records.");
            }

            numberPrefix = meta.NumberPrefix;
        }

        var accessor = TableAccessors.For(table);
        var entity = accessor.Create(table);

        // Defaults of custom fields that were not provided.
        var effective = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in table.Fields.Where(f => f.IsCustom && f.DefaultValue is not null))
        {
            effective[field.Name] = field.DefaultValue;
        }

        foreach (var (key, value) in values)
        {
            effective[key] = value;
        }

        var applied = await ApplyValuesAsync(db, table, accessor, entity, effective, cancellationToken);
        if (!applied.Succeeded)
        {
            return Result<Guid>.From(applied);
        }

        var current = RecordEntityAccessor.ReadValues(table, entity);
        var missing = table.Fields.FirstOrDefault(f => f.IsRequired && !f.IsReadOnly && ConditionValueConverter.IsEmpty(current.GetValueOrDefault(f.Name)));
        if (missing is not null)
        {
            return Result<Guid>.Invalid($"'{missing.Label}' is mandatory.");
        }

        if (table.FindField(CustomTableDefinitionBuilder.NumberField) is not null && !string.IsNullOrWhiteSpace(numberPrefix)
            && ConditionValueConverter.IsEmpty(current.GetValueOrDefault(CustomTableDefinitionBuilder.NumberField)))
        {
            var number = await numbers.NextAsync(table.Name, numberPrefix, cancellationToken);
            if (entity is CustomRecord customRecord)
            {
                customRecord.Number = number;
            }
            else
            {
                entity.GetType().GetProperty(CustomTableDefinitionBuilder.NumberField)?.SetValue(entity, number);
            }
        }

        UpdateDisplayValue(table, entity);
        accessor.Add(db, entity);
        var saved = await SaveAsync(db, cancellationToken);
        if (!saved.Succeeded)
        {
            return Result<Guid>.From(saved);
        }

        var id = ((Entity)entity).Id;
        await IndexAsync(db, table, id, entity, cancellationToken);
        return Result<Guid>.Success(id);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(string tableName, Guid id, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var table = await registry.GetTableAsync(tableName, cancellationToken);
        if (table is null)
        {
            return Result.NotFound($"Table '{tableName}' does not exist.");
        }

        if (!await access.IsAllowedAsync(table.Name, TableOperation.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var accessor = TableAccessors.For(table);
        var entity = await accessor.FindAsync(db, table, id, tracking: true, cancellationToken);
        if (entity is null)
        {
            return Result.NotFound();
        }

        var applied = await ApplyValuesAsync(db, table, accessor, entity, values, cancellationToken);
        if (!applied.Succeeded)
        {
            return applied;
        }

        var current = RecordEntityAccessor.ReadValues(table, entity);
        foreach (var key in values.Keys)
        {
            if (table.FindField(key) is { IsRequired: true } field && ConditionValueConverter.IsEmpty(current.GetValueOrDefault(field.Name)))
            {
                return Result.Invalid($"'{field.Label}' is mandatory.");
            }
        }

        UpdateDisplayValue(table, entity);
        var saved = await SaveAsync(db, cancellationToken);
        if (!saved.Succeeded)
        {
            return saved;
        }

        await IndexAsync(db, table, id, entity, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(string tableName, Guid id, CancellationToken cancellationToken = default)
    {
        var table = await registry.GetTableAsync(tableName, cancellationToken);
        if (table is null)
        {
            return Result.NotFound($"Table '{tableName}' does not exist.");
        }

        if (!await access.IsAllowedAsync(table.Name, TableOperation.Delete, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var accessor = TableAccessors.For(table);
        var entity = await accessor.FindAsync(db, table, id, tracking: true, cancellationToken);
        if (entity is null)
        {
            return Result.NotFound();
        }

        var display = RecordEntityAccessor.ReadValues(table, entity).GetValueOrDefault(table.DisplayField);
        accessor.Remove(db, entity);
        var saved = await SaveAsync(db, cancellationToken);
        if (!saved.Succeeded)
        {
            return saved;
        }

        await RecordFieldIndexer.RemoveAsync(db, table.Name, id, cancellationToken);
        await audit.WriteAsync(new AuditEntry("records", "record.deleted")
        {
            TargetType = table.Name,
            TargetId = id.ToString(),
            TargetName = ConditionValueConverter.ToText(display),
            Message = $"{table.Label} deleted through generic record access.",
        }, cancellationToken);
        return Result.Success();
    }

    private static string? ValidateFieldList(TableDefinition table, IReadOnlyList<string> fields)
    {
        if (fields.Count > 200)
        {
            return "At most 200 fields can be requested.";
        }

        var unknown = fields.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f) && table.FindField(f) is null);
        return unknown is null ? null : $"Unknown field '{unknown}'.";
    }

    private static IReadOnlyList<FieldDefinition> SelectFields(TableDefinition table, IReadOnlyList<string> requested)
    {
        if (requested.Count == 0)
        {
            return table.Fields;
        }

        var selected = requested.Select(table.FindField).Where(f => f is not null).Cast<FieldDefinition>().DistinctBy(f => f.Name).ToList();
        return selected;
    }

    private static Dictionary<string, object?> Project(IReadOnlyDictionary<string, object?> values, IReadOnlyList<FieldDefinition> fields)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(Entity.Id)] = values.GetValueOrDefault(nameof(Entity.Id)),
        };
        foreach (var field in fields)
        {
            row[field.Name] = values.GetValueOrDefault(field.Name);
        }

        return row;
    }

    private static void UpdateDisplayValue(TableDefinition table, object entity)
    {
        if (entity is not CustomRecord record)
        {
            return;
        }

        var display = string.Equals(table.DisplayField, CustomTableDefinitionBuilder.NumberField, StringComparison.OrdinalIgnoreCase)
            ? record.Number
            : ConditionValueConverter.ToText(RecordEntityAccessor.ReadValues(table, record).GetValueOrDefault(table.DisplayField));
        record.DisplayValue = display is { Length: > 255 } ? display[..255] : display;
    }

    private async Task<Result> ApplyValuesAsync(IAppDbContext db, TableDefinition table, ITableAccessor accessor, object entity, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken)
    {
        if (values.Count > MaxValuesPerWrite)
        {
            return Result.Invalid($"At most {MaxValuesPerWrite} fields can be written at once.");
        }

        var references = new List<(FieldDefinition Field, Guid Id)>();
        foreach (var (key, raw) in values)
        {
            if (key.EndsWith(DisplaySuffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var field = table.FindField(key);
            if (field is null)
            {
                return Result.Invalid($"Unknown field '{key}'.");
            }

            if (!access.IsFieldWritable(table, field))
            {
                return Result.Invalid($"'{field.Label}' is read-only.");
            }

            var clrType = RecordEntityAccessor.GetClrType(table, field);
            if (!RecordValueConverter.TryConvert(raw, field, clrType, out var value, out var error))
            {
                return Result.Invalid(error!);
            }

            if (value is string text && accessor.GetMaxLength(db, field) is { } maxLength && text.Length > maxLength)
            {
                return Result.Invalid($"'{field.Label}' must not exceed {maxLength} characters.");
            }

            if (field.Type == FieldType.Reference && value is Guid reference)
            {
                references.Add((field, reference));
            }

            if (!RecordEntityAccessor.WriteValue(entity, field, value))
            {
                return Result.Invalid($"'{field.Label}' is read-only.");
            }
        }

        foreach (var group in references.GroupBy(r => r.Field.ReferenceTable ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var target = await registry.GetTableAsync(group.Key, cancellationToken);
            if (target is null)
            {
                continue;
            }

            var existing = await TableAccessors.For(target).FindExistingAsync(db, target, group.Select(r => r.Id).ToList(), cancellationToken);
            var dangling = group.FirstOrDefault(r => !existing.Contains(r.Id));
            if (dangling.Field is not null)
            {
                return Result.Invalid($"'{dangling.Field.Label}' references a record that does not exist.");
            }
        }

        switch (entity)
        {
            case CustomRecord { DataJson.Length: > MaxDataJsonLength }:
                return Result.Invalid("The record is too large.");
            case IHasCustomFields { CustomFieldsJson.Length: > MaxCustomFieldsJsonLength }:
                return Result.Invalid("The custom field values are too large.");
        }

        return Result.Success();
    }

    private async Task<Result> SaveAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (RecordSaveAbortedException ex)
        {
            return Result.Invalid(ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("The record was changed by someone else. Reload it and try again.");
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Generic record write failed.");
            return Result.Failure("The record could not be saved. Check unique values and references to other records.");
        }
    }

    private async Task IndexAsync(IAppDbContext db, TableDefinition table, Guid id, object entity, CancellationToken cancellationToken)
    {
        if (!table.Fields.Any(f => f.IsCustom))
        {
            return;
        }

        try
        {
            // Read the entity again after saving: before-hooks (business rules) may have changed values.
            await RecordFieldIndexer.SyncAsync(db, table, id, RecordEntityAccessor.ReadValues(table, entity), cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // The record itself is saved; the asynchronous index handler repairs the index from the record event.
            logger.LogWarning(ex, "Field index update failed for {Table}/{RecordId}.", table.Name, id);
        }
    }

    private async Task AddDisplayValuesAsync(IAppDbContext db, IReadOnlyList<FieldDefinition> fields, List<Dictionary<string, object?>> rows, CancellationToken cancellationToken)
    {
        foreach (var field in fields.Where(f => f.Type == FieldType.Choice && f.Choices.Count > 0))
        {
            foreach (var row in rows)
            {
                var value = row.GetValueOrDefault(field.Name);
                if (value is null)
                {
                    continue;
                }

                var key = value is Enum ? Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture) : ConditionValueConverter.ToText(value);
                row[field.Name + DisplaySuffix] = field.Choices.FirstOrDefault(c => string.Equals(c.Value, key, StringComparison.OrdinalIgnoreCase))?.Label ?? key;
            }
        }

        foreach (var group in fields.Where(f => f.Type == FieldType.Reference && f.ReferenceTable is not null).GroupBy(f => f.ReferenceTable!, StringComparer.OrdinalIgnoreCase))
        {
            var ids = rows.SelectMany(r => group.Select(f => r.GetValueOrDefault(f.Name)))
                .Select(v => ConditionValueConverter.TryGetGuid(v, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .Distinct()
                .Take(MaxReferenceIds)
                .ToList();
            if (ids.Count == 0)
            {
                continue;
            }

            var displays = await ResolveDisplayValuesAsync(db, group.Key, ids, cancellationToken);
            foreach (var row in rows)
            {
                foreach (var field in group)
                {
                    if (ConditionValueConverter.TryGetGuid(row.GetValueOrDefault(field.Name), out var id) && displays.TryGetValue(id, out var display))
                    {
                        row[field.Name + DisplaySuffix] = display;
                    }
                }
            }
        }
    }

    private async Task<Dictionary<Guid, string>> ResolveDisplayValuesAsync(IAppDbContext db, string tableName, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, string>();
        var target = await registry.GetTableAsync(tableName, cancellationToken);
        if (target is null)
        {
            return result;
        }

        // Names of users and groups are directory information available to every signed-in user (pickers);
        // display values of other tables require read access to that table.
        var isDirectory = string.Equals(target.Name, TableNames.User, StringComparison.OrdinalIgnoreCase) || string.Equals(target.Name, TableNames.Group, StringComparison.OrdinalIgnoreCase);
        if (!isDirectory && !await access.IsAllowedAsync(target.Name, TableOperation.Read, cancellationToken))
        {
            return result;
        }

        var entities = await TableAccessors.For(target).LoadManyAsync(db, target, ids, cancellationToken);
        foreach (var entity in entities)
        {
            var values = RecordEntityAccessor.ReadValues(target, entity);
            var display = ConditionValueConverter.ToText(values.GetValueOrDefault(target.DisplayField));
            result[((Entity)entity).Id] = string.IsNullOrWhiteSpace(display) ? ((Entity)entity).Id.ToString() : display;
        }

        return result;
    }
}
