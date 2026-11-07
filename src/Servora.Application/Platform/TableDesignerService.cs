using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.BusinessRules;
using Servora.Domain.Common;
using Servora.Domain.Notifications;
using Servora.Domain.Platform;
using Servora.Domain.Sla;

namespace Servora.Application.Platform;

/// <summary>Default <see cref="ITableDesignerService"/>. Every change is audited and invalidates the table registry cache.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="registry">The table registry.</param>
/// <param name="access">The table access service.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="audit">The audit service.</param>
public sealed partial class TableDesignerService(
    IAppDbContextFactory dbFactory,
    ITableRegistry registry,
    ITableAccessService access,
    ICurrentUser currentUser,
    IAuditService audit) : ITableDesignerService
{
    /// <summary>The maximum number of custom fields per table.</summary>
    public const int MaxFieldsPerTable = 200;

    /// <summary>The maximum number of choices per choice field.</summary>
    public const int MaxChoices = 200;

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<TableSummary>>> ListTablesAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageTables, cancellationToken))
        {
            return Result<IReadOnlyList<TableSummary>>.Forbidden();
        }

        var tables = await registry.GetTablesAsync(cancellationToken);
        return Result<IReadOnlyList<TableSummary>>.Success(tables.Select(ToSummary).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<CustomTableInput>> GetCustomTableAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageTables, cancellationToken))
        {
            return Result<CustomTableInput>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var table = await db.Set<CustomTable>().AsNoTracking().FirstOrDefaultAsync(t => t.Name == name, cancellationToken);
        return table is null
            ? Result<CustomTableInput>.NotFound("The custom table was not found.")
            : Result<CustomTableInput>.Success(new CustomTableInput
            {
                Name = table.Name,
                Label = table.Label,
                PluralLabel = table.PluralLabel,
                Description = table.Description,
                Icon = table.Icon,
                DisplayField = table.DisplayField,
                NumberPrefix = table.NumberPrefix,
                IsActive = table.IsActive,
            });
    }

    /// <inheritdoc />
    public async Task<Result<string>> CreateTableAsync(CustomTableInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageTables, cancellationToken))
        {
            return Result<string>.Forbidden();
        }

        var name = input.Name.Trim().ToLowerInvariant();
        if (!NamePattern().IsMatch(name))
        {
            return Result<string>.Invalid("The table name must start with 'u_' followed by 2-40 lower-case letters, digits or underscores.");
        }

        if (await registry.GetTableAsync(name, cancellationToken) is not null)
        {
            return Result<string>.Conflict($"A table named '{name}' already exists.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await ValidateTableInputAsync(db, input, null, cancellationToken) is { } error)
        {
            return Result<string>.Invalid(error);
        }

        var table = new CustomTable { Name = name };
        Apply(table, input);
        db.Set<CustomTable>().Add(table);
        await db.SaveChangesAsync(cancellationToken);
        await registry.InvalidateAsync();
        await AuditAsync("table.created", name, table.Label, cancellationToken);
        return Result<string>.Success(name);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateTableAsync(string name, CustomTableInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageTables, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var table = await db.Set<CustomTable>().FirstOrDefaultAsync(t => t.Name == name, cancellationToken);
        if (table is null)
        {
            return Result.NotFound("The custom table was not found.");
        }

        if (await ValidateTableInputAsync(db, input, table, cancellationToken) is { } error)
        {
            return Result.Invalid(error);
        }

        if (!string.IsNullOrWhiteSpace(input.DisplayField))
        {
            var definition = await registry.GetTableAsync(name, cancellationToken);
            if (definition?.FindField(input.DisplayField) is null)
            {
                return Result.Invalid($"The display field '{input.DisplayField}' does not exist.");
            }
        }

        var previousPrefix = table.NumberPrefix;
        Apply(table, input);
        if (!string.Equals(previousPrefix, table.NumberPrefix, StringComparison.Ordinal) && table.NumberPrefix is not null)
        {
            var sequence = await db.Set<NumberSequence>().FirstOrDefaultAsync(s => s.TableName == name, cancellationToken);
            if (sequence is not null)
            {
                sequence.Prefix = table.NumberPrefix;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await registry.InvalidateAsync();
        await AuditAsync("table.updated", name, table.Label, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteTableAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageTables, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var table = await db.Set<CustomTable>().FirstOrDefaultAsync(t => t.Name == name, cancellationToken);
        if (table is null)
        {
            return Result.NotFound("The custom table was not found.");
        }

        var dependencies = await db.Set<BusinessRule>().CountAsync(r => r.TableName == name, cancellationToken)
            + await db.Set<SlaDefinition>().CountAsync(s => s.TableName == name, cancellationToken)
            + await db.Set<NotificationDefinition>().CountAsync(n => n.TableName == name, cancellationToken)
            + await db.Set<CustomField>().CountAsync(f => f.ReferenceTable == name && f.TableName != name, cancellationToken);
        if (dependencies > 0)
        {
            return Result.Invalid("The table is used by business rules, SLA definitions, notifications or reference fields of other tables. Remove them first.");
        }

        await db.Set<RecordFieldValue>().Where(v => v.TableName == name).ExecuteDeleteAsync(cancellationToken);
        await db.Set<CustomRecord>().Where(r => r.TableName == name).ExecuteDeleteAsync(cancellationToken);
        await db.Set<CustomField>().Where(f => f.TableName == name).ExecuteDeleteAsync(cancellationToken);
        await db.Set<TableLayout>().Where(l => l.TableName == name).ExecuteDeleteAsync(cancellationToken);
        db.Set<CustomTable>().Remove(table);
        await db.SaveChangesAsync(cancellationToken);
        await registry.InvalidateAsync();
        await AuditAsync("table.deleted", name, table.Label, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<CustomFieldInput>> GetFieldAsync(string tableName, string fieldName, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageTables, cancellationToken))
        {
            return Result<CustomFieldInput>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var field = await db.Set<CustomField>().AsNoTracking().FirstOrDefaultAsync(f => f.TableName == tableName && f.Name == fieldName, cancellationToken);
        return field is null
            ? Result<CustomFieldInput>.NotFound("The custom field was not found.")
            : Result<CustomFieldInput>.Success(new CustomFieldInput
            {
                Name = field.Name,
                Label = field.Label,
                Type = (FieldType)(int)field.Type,
                Choices = [.. CustomTableDefinitionBuilder.ParseChoices(field.ChoicesJson)],
                ReferenceTable = field.ReferenceTable,
                IsRequired = field.IsRequired,
                DefaultValue = field.DefaultValue,
                HelpText = field.HelpText,
                MaxLength = field.MaxLength,
                Order = field.Order,
            });
    }

    /// <inheritdoc />
    public async Task<Result<string>> AddFieldAsync(string tableName, CustomFieldInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageTables, cancellationToken))
        {
            return Result<string>.Forbidden();
        }

        var table = await registry.GetTableAsync(tableName, cancellationToken);
        if (table is null)
        {
            return Result<string>.NotFound($"Table '{tableName}' does not exist.");
        }

        if (!table.IsCustom && (table.EntityType is null || !typeof(IHasCustomFields).IsAssignableFrom(table.EntityType)))
        {
            return Result<string>.Invalid($"The table '{table.Label}' does not support custom fields.");
        }

        var name = input.Name.Trim().ToLowerInvariant();
        if (!NamePattern().IsMatch(name))
        {
            return Result<string>.Invalid("The field name must start with 'u_' followed by 2-40 lower-case letters, digits or underscores.");
        }

        if (table.FindField(name) is not null)
        {
            return Result<string>.Conflict($"The table already has a field named '{name}'.");
        }

        if (table.Fields.Count(f => f.IsCustom) >= MaxFieldsPerTable)
        {
            return Result<string>.Invalid($"A table can have at most {MaxFieldsPerTable} custom fields.");
        }

        if (await ValidateFieldInputAsync(input, input.Type, cancellationToken) is { } error)
        {
            return Result<string>.Invalid(error);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var field = new CustomField
        {
            TableName = table.Name,
            Name = name,
            Type = (CustomFieldType)(int)input.Type,
            Order = input.Order == 0 ? (table.Fields.Count(f => f.IsCustom) + 1) * 10 : input.Order,
        };
        Apply(field, input);
        db.Set<CustomField>().Add(field);
        await db.SaveChangesAsync(cancellationToken);
        await registry.InvalidateAsync();
        await AuditAsync("field.created", $"{table.Name}.{name}", field.Label, cancellationToken);
        return Result<string>.Success(name);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateFieldAsync(string tableName, string fieldName, CustomFieldInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageTables, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var field = await db.Set<CustomField>().FirstOrDefaultAsync(f => f.TableName == tableName && f.Name == fieldName, cancellationToken);
        if (field is null)
        {
            return Result.NotFound("The custom field was not found.");
        }

        var type = (FieldType)(int)field.Type;
        if (input.Type != type)
        {
            return Result.Invalid("The type of an existing field cannot be changed. Create a new field instead.");
        }

        if (await ValidateFieldInputAsync(input, type, cancellationToken) is { } error)
        {
            return Result.Invalid(error);
        }

        Apply(field, input);
        if (input.Order != 0)
        {
            field.Order = input.Order;
        }

        await db.SaveChangesAsync(cancellationToken);
        await registry.InvalidateAsync();
        await AuditAsync("field.updated", $"{tableName}.{fieldName}", field.Label, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteFieldAsync(string tableName, string fieldName, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageTables, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var field = await db.Set<CustomField>().FirstOrDefaultAsync(f => f.TableName == tableName && f.Name == fieldName, cancellationToken);
        if (field is null)
        {
            return Result.NotFound("The custom field was not found.");
        }

        await db.Set<RecordFieldValue>().Where(v => v.TableName == tableName && v.Field == fieldName).ExecuteDeleteAsync(cancellationToken);
        db.Set<CustomField>().Remove(field);
        await db.SaveChangesAsync(cancellationToken);
        await registry.InvalidateAsync();
        await AuditAsync("field.deleted", $"{tableName}.{fieldName}", field.Label, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<TableLayoutModel>> GetLayoutAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var table = await registry.GetTableAsync(tableName, cancellationToken);
        if (table is null)
        {
            return Result<TableLayoutModel>.NotFound($"Table '{tableName}' does not exist.");
        }

        if (!await access.IsAllowedAsync(table.Name, TableOperation.Read, cancellationToken)
            && !await currentUser.HasPermissionAsync(Permissions.Platform.ManageTables, cancellationToken))
        {
            return Result<TableLayoutModel>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var stored = await db.Set<TableLayout>().AsNoTracking().FirstOrDefaultAsync(l => l.TableName == table.Name, cancellationToken);
        return Result<TableLayoutModel>.Success(BuildLayout(table, stored));
    }

    /// <inheritdoc />
    public async Task<Result> SaveLayoutAsync(string tableName, TableLayoutModel layout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageTables, cancellationToken))
        {
            return Result.Forbidden();
        }

        var table = await registry.GetTableAsync(tableName, cancellationToken);
        if (table is null)
        {
            return Result.NotFound($"Table '{tableName}' does not exist.");
        }

        if (layout.Sections.Count > 30 || layout.Sections.Sum(s => s.Fields.Count) > 400 || layout.ListColumns.Count > 30)
        {
            return Result.Invalid("The layout is too large (at most 30 sections, 400 fields and 30 list columns).");
        }

        var unknown = layout.Sections.SelectMany(s => s.Fields).Concat(layout.ListColumns).FirstOrDefault(f => table.FindField(f) is null);
        if (unknown is not null)
        {
            return Result.Invalid($"Unknown field '{unknown}'.");
        }

        if (layout.Sections.Any(s => string.IsNullOrWhiteSpace(s.Title) || s.Title.Length > 100))
        {
            return Result.Invalid("Every section needs a title of at most 100 characters.");
        }

        if (layout.ListColumns.Count == 0)
        {
            return Result.Invalid("Select at least one list column.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var stored = await db.Set<TableLayout>().FirstOrDefaultAsync(l => l.TableName == table.Name, cancellationToken);
        if (stored is null)
        {
            stored = new TableLayout { TableName = table.Name };
            db.Set<TableLayout>().Add(stored);
        }

        var sections = layout.Sections.Select(s => new FormSectionModel { Title = s.Title.Trim(), Fields = s.Fields.Select(f => table.FindField(f)!.Name).Distinct().ToList() }).ToList();
        stored.FormJson = JsonSerializer.Serialize(sections, s_json);
        stored.ListColumnsJson = JsonSerializer.Serialize(layout.ListColumns.Select(f => table.FindField(f)!.Name).Distinct().ToList(), s_json);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync("layout.updated", table.Name, table.Label, cancellationToken);
        return Result.Success();
    }

    /// <summary>Builds the effective layout of a table from the stored layout or a generated default.</summary>
    /// <param name="table">The table.</param>
    /// <param name="stored">The stored layout, if any.</param>
    /// <returns>The layout restricted to existing fields.</returns>
    public static TableLayoutModel BuildLayout(TableDefinition table, TableLayout? stored)
    {
        var layout = new TableLayoutModel();
        if (stored?.FormJson is { Length: > 0 } formJson)
        {
            try
            {
                layout.Sections = (JsonSerializer.Deserialize<List<FormSectionModel>>(formJson, s_json) ?? [])
                    .Select(s => new FormSectionModel { Title = s.Title, Fields = s.Fields.Where(f => table.FindField(f) is not null).ToList() })
                    .ToList();
                layout.IsCustomized = true;
            }
            catch (JsonException)
            {
                layout.Sections = [];
            }
        }

        if (stored?.ListColumnsJson is { Length: > 0 } listJson)
        {
            try
            {
                layout.ListColumns = (JsonSerializer.Deserialize<List<string>>(listJson, s_json) ?? []).Where(f => table.FindField(f) is not null).ToList();
                layout.IsCustomized = true;
            }
            catch (JsonException)
            {
                layout.ListColumns = [];
            }
        }

        if (layout.Sections.Count == 0)
        {
            var main = table.Fields.Where(f => !f.IsReadOnly && !f.IsCustom).Select(f => f.Name).ToList();
            var custom = table.Fields.Where(f => f.IsCustom).Select(f => f.Name).ToList();
            if (table.IsCustom)
            {
                layout.Sections.Add(new FormSectionModel { Title = "General", Fields = custom });
            }
            else
            {
                layout.Sections.Add(new FormSectionModel { Title = "General", Fields = main });
                if (custom.Count > 0)
                {
                    layout.Sections.Add(new FormSectionModel { Title = "Additional information", Fields = custom });
                }
            }
        }
        else
        {
            // Fields added after the layout was saved appear in a trailing section so they are never hidden.
            var placed = layout.Sections.SelectMany(s => s.Fields).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = table.Fields.Where(f => f.IsCustom && !placed.Contains(f.Name)).Select(f => f.Name).ToList();
            if (missing.Count > 0)
            {
                layout.Sections.Add(new FormSectionModel { Title = "New fields", Fields = missing });
            }
        }

        if (layout.ListColumns.Count == 0)
        {
            var columns = new List<string> { table.FindField(table.DisplayField)?.Name ?? table.Fields[0].Name };
            columns.AddRange(table.Fields
                .Where(f => !columns.Contains(f.Name, StringComparer.OrdinalIgnoreCase) && !f.IsReadOnly
                    && f.Type is FieldType.String or FieldType.Choice or FieldType.Reference or FieldType.Boolean or FieldType.DateTime or FieldType.Date or FieldType.Integer or FieldType.Email)
                .Take(5)
                .Select(f => f.Name));
            if (table.FindField(nameof(AuditableEntity.UpdatedAt)) is { } updated)
            {
                columns.Add(updated.Name);
            }

            layout.ListColumns = columns;
        }

        return layout;
    }

    private static TableSummary ToSummary(TableDefinition table) => new(
        table.Name,
        table.Label,
        table.PluralLabel,
        table.Icon,
        table.IsCustom,
        table.Fields.Count,
        table.Fields.Count(f => f.IsCustom),
        table.IsCustom || (table.EntityType is not null && typeof(IHasCustomFields).IsAssignableFrom(table.EntityType)),
        table.Route);

    private static void Apply(CustomTable table, CustomTableInput input)
    {
        table.Label = input.Label.Trim();
        table.PluralLabel = string.IsNullOrWhiteSpace(input.PluralLabel) ? table.Label + "s" : input.PluralLabel.Trim();
        table.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        table.Icon = string.IsNullOrWhiteSpace(input.Icon) ? "table" : input.Icon.Trim();
        table.DisplayField = string.IsNullOrWhiteSpace(input.DisplayField) ? null : input.DisplayField.Trim();
        table.NumberPrefix = string.IsNullOrWhiteSpace(input.NumberPrefix) ? null : input.NumberPrefix.Trim().ToUpperInvariant();
        table.IsActive = input.IsActive;
    }

    private static void Apply(CustomField field, CustomFieldInput input)
    {
        field.Label = input.Label.Trim();
        field.ChoicesJson = input.Type == FieldType.Choice
            ? CustomTableDefinitionBuilder.SerializeChoices(input.Choices.Select(c => new FieldChoice(c.Value.Trim(), string.IsNullOrWhiteSpace(c.Label) ? c.Value.Trim() : c.Label.Trim())))
            : null;
        field.ReferenceTable = input.Type == FieldType.Reference ? input.ReferenceTable?.Trim() : null;
        field.IsRequired = input.IsRequired;
        field.DefaultValue = string.IsNullOrWhiteSpace(input.DefaultValue) ? null : input.DefaultValue.Trim();
        field.HelpText = string.IsNullOrWhiteSpace(input.HelpText) ? null : input.HelpText.Trim();
        field.MaxLength = input.Type is FieldType.String or FieldType.Text ? input.MaxLength : null;
    }

    private static async Task<string?> ValidateTableInputAsync(IAppDbContext db, CustomTableInput input, CustomTable? existing, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Label) || input.Label.Trim().Length > 100)
        {
            return "The label is required and must not exceed 100 characters.";
        }

        if (input.PluralLabel is { Length: > 100 })
        {
            return "The plural label must not exceed 100 characters.";
        }

        if (input.Description is { Length: > 1000 })
        {
            return "The description must not exceed 1000 characters.";
        }

        if (!string.IsNullOrWhiteSpace(input.Icon) && !IconPattern().IsMatch(input.Icon.Trim()))
        {
            return "The icon name may only contain lower-case letters, digits and dashes.";
        }

        if (!string.IsNullOrWhiteSpace(input.NumberPrefix))
        {
            var prefix = input.NumberPrefix.Trim().ToUpperInvariant();
            if (!PrefixPattern().IsMatch(prefix))
            {
                return "The number prefix must consist of 2-8 letters.";
            }

            if (TablePolicies.NumberPrefixes.ContainsKey(prefix))
            {
                return $"The number prefix '{prefix}' is reserved for a built-in table.";
            }

            var existingId = existing?.Id ?? Guid.Empty;
            if (await db.Set<CustomTable>().AnyAsync(t => t.NumberPrefix == prefix && t.Id != existingId, cancellationToken))
            {
                return $"The number prefix '{prefix}' is already used by another table.";
            }
        }

        return null;
    }

    private async Task<string?> ValidateFieldInputAsync(CustomFieldInput input, FieldType type, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(type))
        {
            return "Unknown field type.";
        }

        if (string.IsNullOrWhiteSpace(input.Label) || input.Label.Trim().Length > 100)
        {
            return "The label is required and must not exceed 100 characters.";
        }

        if (input.HelpText is { Length: > 1000 })
        {
            return "The help text must not exceed 1000 characters.";
        }

        if (type is FieldType.String or FieldType.Text && input.MaxLength is { } maxLength
            && (maxLength < 1 || maxLength > (type == FieldType.String ? 1000 : 16_000)))
        {
            return type == FieldType.String ? "The maximum length must be between 1 and 1000." : "The maximum length must be between 1 and 16000.";
        }

        if (type == FieldType.Choice)
        {
            var choices = input.Choices.Where(c => !string.IsNullOrWhiteSpace(c.Value)).ToList();
            if (choices.Count == 0 || choices.Count > MaxChoices)
            {
                return $"A choice field needs between 1 and {MaxChoices} choices.";
            }

            if (choices.Any(c => c.Value.Trim().Length > 100 || (c.Label?.Length ?? 0) > 100))
            {
                return "Choice values and labels must not exceed 100 characters.";
            }

            if (choices.Select(c => c.Value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != choices.Count)
            {
                return "Choice values must be unique.";
            }

            input.Choices = choices;
        }

        if (type == FieldType.Reference)
        {
            if (string.IsNullOrWhiteSpace(input.ReferenceTable) || await registry.GetTableAsync(input.ReferenceTable.Trim(), cancellationToken) is null)
            {
                return "Select an existing table for the reference field.";
            }
        }

        if (!string.IsNullOrWhiteSpace(input.DefaultValue))
        {
            var probe = new FieldDefinition
            {
                Name = "probe",
                Label = "Default value",
                Type = type,
                Choices = input.Choices.Select(c => new FieldChoice(c.Value.Trim(), c.Label ?? c.Value)).ToList(),
            };
            if (input.DefaultValue.Length > 1000)
            {
                return "The default value must not exceed 1000 characters.";
            }

            if (!RecordValueConverter.TryConvert(input.DefaultValue, probe, null, out _, out var error))
            {
                return error;
            }
        }

        return null;
    }

    private Task AuditAsync(string action, string target, string? name, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEntry("platform", action) { TargetType = "sys_db_object", TargetId = target, TargetName = name }, cancellationToken);

    [GeneratedRegex("^u_[a-z0-9_]{2,40}$")]
    private static partial Regex NamePattern();

    [GeneratedRegex("^[a-z0-9-]{1,40}$")]
    private static partial Regex IconPattern();

    [GeneratedRegex("^[A-Z]{2,8}$")]
    private static partial Regex PrefixPattern();
}
