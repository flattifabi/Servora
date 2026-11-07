using System.Globalization;
using Servora.Application.Conditions;
using Servora.Domain.Platform;

namespace Servora.Application.Tables;

/// <summary>
/// Single source of truth for how field values are written into, and compared against, the
/// <see cref="RecordFieldValue"/> index. Used by the indexer and by the condition expression builder.
/// </summary>
public static class FieldIndexNormalizer
{
    /// <summary>Gets the index column used for a field type.</summary>
    /// <param name="type">The field type.</param>
    /// <returns>The column.</returns>
    public static FieldIndexColumn ColumnFor(FieldType type) => type switch
    {
        FieldType.Integer or FieldType.Decimal => FieldIndexColumn.Number,
        FieldType.DateTime or FieldType.Date => FieldIndexColumn.Date,
        _ => FieldIndexColumn.Text,
    };

    /// <summary>Normalizes text for the index: lower case (invariant) and truncated to <see cref="RecordFieldValue.MaxTextLength"/>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The normalized text.</returns>
    public static string NormalizeText(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower.Length > RecordFieldValue.MaxTextLength ? lower[..RecordFieldValue.MaxTextLength] : lower;
    }

    /// <summary>Fills an index row from a field value. Returns <see langword="false"/> for empty values (no row is stored).</summary>
    /// <param name="type">The field type.</param>
    /// <param name="value">The (typed or JSON-normalized) value.</param>
    /// <param name="row">The row to fill.</param>
    /// <returns><see langword="true"/> when the value is not empty.</returns>
    public static bool Fill(FieldType type, object? value, RecordFieldValue row)
    {
        value = ConditionValueConverter.Normalize(value);
        if (ConditionValueConverter.IsEmpty(value))
        {
            return false;
        }

        row.TextValue = null;
        row.NumberValue = null;
        row.DateValue = null;
        switch (ColumnFor(type))
        {
            case FieldIndexColumn.Number:
                if (value is IConvertible && double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    row.NumberValue = number;
                    return true;
                }

                return false;
            case FieldIndexColumn.Date:
                if (value is DateTimeOffset dto)
                {
                    row.DateValue = dto.ToUniversalTime();
                    return true;
                }

                if (value is DateOnly date)
                {
                    row.DateValue = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                    return true;
                }

                if (ConditionValueConverter.TryParseDate(ConditionValueConverter.ToText(value), out var parsed, out _))
                {
                    row.DateValue = parsed;
                    return true;
                }

                return false;
            default:
                var text = value switch
                {
                    bool flag => flag ? "true" : "false",
                    Guid guid => guid.ToString("D"),
                    _ => ConditionValueConverter.ToText(value) ?? string.Empty,
                };
                if (type == FieldType.Boolean && ConditionValueConverter.TryParseBoolean(text, out var boolean))
                {
                    text = boolean ? "true" : "false";
                    row.NumberValue = boolean ? 1 : 0;
                }
                else if (type is FieldType.Reference or FieldType.Guid && Guid.TryParse(text, out var id))
                {
                    text = id.ToString("D");
                }

                row.TextValue = NormalizeText(text);
                return true;
        }
    }

    /// <summary>Normalizes a condition value for comparison with the text column of a field.</summary>
    /// <param name="type">The field type.</param>
    /// <param name="value">The condition value.</param>
    /// <returns>The normalized text, or <see langword="null"/> when the value is invalid for the field type.</returns>
    public static string? NormalizeConditionText(FieldType type, string value)
    {
        if (type == FieldType.Boolean)
        {
            return ConditionValueConverter.TryParseBoolean(value, out var flag) ? (flag ? "true" : "false") : null;
        }

        if (type is FieldType.Reference or FieldType.Guid)
        {
            return Guid.TryParse(value, out var id) ? id.ToString("D") : NormalizeText(value);
        }

        return NormalizeText(value);
    }
}
