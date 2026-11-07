using Servora.Application.Conditions;
using Servora.Application.Events;
using Servora.Domain.Common;
using Servora.Domain.Platform;

namespace Servora.Application.Tables;

/// <summary>
/// Expands changes of the JSON storage columns (<see cref="CustomRecord.DataJson"/>,
/// <see cref="IHasCustomFields.CustomFieldsJson"/>) into one change per custom field, so change-aware condition
/// operators work for custom fields exactly like for typed properties.
/// </summary>
public static class RecordChangeExpander
{
    private static readonly string[] s_jsonColumns = [nameof(CustomRecord.DataJson), nameof(IHasCustomFields.CustomFieldsJson)];

    /// <summary>Expands the changes.</summary>
    /// <param name="changes">The raw changes.</param>
    /// <returns>The expanded changes.</returns>
    public static IReadOnlyList<RecordFieldChange> Expand(IReadOnlyList<RecordFieldChange> changes)
    {
        if (!changes.Any(c => s_jsonColumns.Contains(c.Field, StringComparer.OrdinalIgnoreCase)))
        {
            return changes;
        }

        var result = new List<RecordFieldChange>(changes.Count + 8);
        foreach (var change in changes)
        {
            if (!s_jsonColumns.Contains(change.Field, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(change);
                continue;
            }

            var before = RecordEntityAccessor.ParseObject(change.OldValue as string);
            var after = RecordEntityAccessor.ParseObject(change.NewValue as string);
            foreach (var key in before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var oldValue = before.GetValueOrDefault(key);
                var newValue = after.GetValueOrDefault(key);
                if (!string.Equals(ConditionValueConverter.ToText(oldValue), ConditionValueConverter.ToText(newValue), StringComparison.Ordinal))
                {
                    result.Add(new RecordFieldChange(key, oldValue, newValue));
                }
            }
        }

        return result;
    }
}
