using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using Servora.Application.Tables;
using Servora.Domain.Common;

namespace Servora.Infrastructure.Platform;

/// <summary>
/// Baseline <see cref="ITableRegistry"/> that discovers typed tables (entities with <see cref="RecordTableAttribute"/>)
/// from the domain assembly by reflection. The Platform module (T-07) replaces it with a registry that also includes
/// custom tables and custom fields, and may reuse <see cref="BuildTypedTable"/>.
/// Properties marked with <see cref="NotExposedAttribute"/>, navigation properties and collections are never exposed.
/// </summary>
public class ReflectionTableRegistry : ITableRegistry
{
    private static readonly string[] s_readOnlyFields = ["Id", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "Version", "Number"];
    private static readonly Lazy<IReadOnlyList<TableDefinition>> s_typedTables = new(DiscoverTypedTables);
    private static readonly ConcurrentDictionary<Type, string?> s_names = new();

    /// <summary>Gets the typed tables discovered from the domain assembly.</summary>
    public static IReadOnlyList<TableDefinition> TypedTables => s_typedTables.Value;

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<TableDefinition>> GetTablesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(TypedTables);

    /// <inheritdoc />
    public virtual async Task<TableDefinition?> GetTableAsync(string tableName, CancellationToken cancellationToken = default) =>
        (await GetTablesAsync(cancellationToken)).FirstOrDefault(t => string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public string? GetTableName(Type entityType) =>
        s_names.GetOrAdd(entityType, t => t.GetCustomAttribute<RecordTableAttribute>(inherit: false)?.Name);

    /// <inheritdoc />
    public virtual Task InvalidateAsync() => Task.CompletedTask;

    /// <summary>Builds the table definition of a typed entity.</summary>
    /// <param name="type">The entity type decorated with <see cref="RecordTableAttribute"/>.</param>
    /// <returns>The table definition.</returns>
    public static TableDefinition BuildTypedTable(Type type)
    {
        var attribute = type.GetCustomAttribute<RecordTableAttribute>(inherit: false)
            ?? throw new ArgumentException($"{type.Name} is not a record table.", nameof(type));
        var fields = new List<FieldDefinition>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<NotExposedAttribute>() is not null || property.GetMethod is null || property.Name == nameof(IHasCustomFields.CustomFieldsJson))
            {
                continue;
            }

            var fieldType = MapType(property, out var choices, out var referenceTable);
            if (fieldType is null)
            {
                continue;
            }

            fields.Add(new FieldDefinition
            {
                Name = property.Name,
                Label = property.GetCustomAttribute<DisplayAttribute>()?.Name ?? Humanize(property.Name),
                Type = fieldType.Value,
                Choices = choices,
                ReferenceTable = referenceTable,
                IsReadOnly = s_readOnlyFields.Contains(property.Name) || property.SetMethod is null,
                IsRequired = property.PropertyType == typeof(string) && Nullable.GetUnderlyingType(property.PropertyType) is null
                    && new NullabilityInfoContext().Create(property).WriteState == NullabilityState.NotNull && !s_readOnlyFields.Contains(property.Name),
            });
        }

        return new TableDefinition
        {
            Name = attribute.Name,
            Label = attribute.Label,
            PluralLabel = attribute.PluralLabel ?? attribute.Label + "s",
            DisplayField = attribute.DisplayField,
            Icon = attribute.Icon,
            Route = attribute.Route,
            EntityType = type,
            Fields = fields,
        };
    }

    /// <summary>Converts a PascalCase property name into a label ("AssignmentGroupId" becomes "Assignment Group").</summary>
    /// <param name="name">The property name.</param>
    /// <returns>The label.</returns>
    public static string Humanize(string name)
    {
        if (name.Length > 2 && name.EndsWith("Id", StringComparison.Ordinal))
        {
            name = name[..^2];
        }

        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && (!char.IsUpper(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
            {
                builder.Append(' ');
            }

            builder.Append(name[i]);
        }

        return builder.ToString();
    }

    private static IReadOnlyList<TableDefinition> DiscoverTypedTables() =>
        typeof(RecordTableAttribute).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetCustomAttribute<RecordTableAttribute>(inherit: false) is not null)
            .Select(BuildTypedTable)
            .OrderBy(t => t.Label)
            .ToList();

    private static FieldType? MapType(PropertyInfo property, out IReadOnlyList<FieldChoice> choices, out string? referenceTable)
    {
        choices = [];
        referenceTable = property.GetCustomAttribute<ReferenceAttribute>()?.Table;
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type.IsEnum)
        {
            choices = Enum.GetValues(type).Cast<object>()
                .Select(v => new FieldChoice(Convert.ToInt32(v).ToString(System.Globalization.CultureInfo.InvariantCulture), Humanize(v.ToString()!)))
                .ToList();
            return FieldType.Choice;
        }

        if (type == typeof(Guid))
        {
            return referenceTable is null ? FieldType.Guid : FieldType.Reference;
        }

        if (type == typeof(string))
        {
            return property.Name.Contains("Description", StringComparison.Ordinal) || property.Name.EndsWith("Text", StringComparison.Ordinal) || property.Name.EndsWith("Json", StringComparison.Ordinal)
                ? FieldType.Text
                : property.Name.Contains("Email", StringComparison.Ordinal) ? FieldType.Email : FieldType.String;
        }

        if (type == typeof(bool))
        {
            return FieldType.Boolean;
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
        {
            return FieldType.Integer;
        }

        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
        {
            return FieldType.Decimal;
        }

        if (type == typeof(DateTimeOffset) || type == typeof(DateTime))
        {
            return FieldType.DateTime;
        }

        if (type == typeof(DateOnly))
        {
            return FieldType.Date;
        }

        return null;
    }
}
