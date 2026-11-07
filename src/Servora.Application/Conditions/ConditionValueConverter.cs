using System.Globalization;
using System.Text.Json;

namespace Servora.Application.Conditions;

/// <summary>
/// Type coercion used by condition evaluation: converts invariant condition strings into CLR values (enums by value
/// or name, <see cref="Guid"/>, dates, numbers, booleans) and compares runtime values with condition strings.
/// </summary>
public static class ConditionValueConverter
{
    private static readonly string[] s_trueValues = ["true", "1", "yes", "on", "y"];
    private static readonly string[] s_falseValues = ["false", "0", "no", "off", "n"];

    /// <summary>Normalizes a runtime value (unwraps <see cref="JsonElement"/>s into primitives).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The normalized value.</returns>
    public static object? Normalize(object? value) => value switch
    {
        JsonElement element => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.TryGetDecimal(out var d) ? d : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText(),
        },
        _ => value,
    };

    /// <summary>Checks whether a value is considered empty (null, blank text or <see cref="Guid.Empty"/>).</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when empty.</returns>
    public static bool IsEmpty(object? value) => Normalize(value) switch
    {
        null => true,
        string s => string.IsNullOrWhiteSpace(s),
        Guid g => g == Guid.Empty,
        _ => false,
    };

    /// <summary>Parses a boolean condition value (<c>true/false</c>, <c>1/0</c>, <c>yes/no</c>, <c>on/off</c>).</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The parsed value.</param>
    /// <returns><see langword="true"/> when the text is a boolean.</returns>
    public static bool TryParseBoolean(string? text, out bool value)
    {
        var trimmed = text?.Trim();
        if (s_trueValues.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        value = false;
        return s_falseValues.Contains(trimmed, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Parses an ISO 8601 date or date/time. Values without offset are interpreted as UTC.</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The parsed value.</param>
    /// <param name="dateOnly">Whether the text contained only a date (no time part).</param>
    /// <returns><see langword="true"/> when the text is a date.</returns>
    public static bool TryParseDate(string? text, out DateTimeOffset value, out bool dateOnly)
    {
        value = default;
        dateOnly = false;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < 8 || !char.IsDigit(trimmed[0]) || !trimmed.Contains('-'))
        {
            return false;
        }

        if (trimmed.Length == 10 && DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            value = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            dateOnly = true;
            return true;
        }

        return DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
    }

    /// <summary>
    /// Converts an invariant condition string into a value of the target type. Enums accept their numeric value or
    /// (case-insensitive) name.
    /// </summary>
    /// <param name="text">The condition value.</param>
    /// <param name="targetType">The target type (nullable types are unwrapped).</param>
    /// <param name="value">The converted value.</param>
    /// <returns><see langword="true"/> when the conversion succeeded.</returns>
    public static bool TryConvert(string? text, Type targetType, out object? value)
    {
        value = null;
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var trimmed = text?.Trim() ?? string.Empty;
        var inv = CultureInfo.InvariantCulture;

        if (type == typeof(string))
        {
            value = text ?? string.Empty;
            return true;
        }

        if (trimmed.Length == 0)
        {
            return false;
        }

        if (type.IsEnum)
        {
            if (long.TryParse(trimmed, NumberStyles.Integer, inv, out var number))
            {
                value = Enum.ToObject(type, number);
                return true;
            }

            if (Enum.TryParse(type, trimmed, ignoreCase: true, out var named) && Enum.IsDefined(type, named!))
            {
                value = named;
                return true;
            }

            var compact = trimmed.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (Enum.TryParse(type, compact, ignoreCase: true, out named) && Enum.IsDefined(type, named!))
            {
                value = named;
                return true;
            }

            return false;
        }

        if (type == typeof(Guid))
        {
            if (Guid.TryParse(trimmed, out var guid))
            {
                value = guid;
                return true;
            }

            return false;
        }

        if (type == typeof(bool))
        {
            if (TryParseBoolean(trimmed, out var flag))
            {
                value = flag;
                return true;
            }

            return false;
        }

        if (type == typeof(DateTimeOffset) || type == typeof(DateTime) || type == typeof(DateOnly))
        {
            if (!TryParseDate(trimmed, out var date, out _))
            {
                return false;
            }

            value = type == typeof(DateTimeOffset) ? date : type == typeof(DateTime) ? date.UtcDateTime : DateOnly.FromDateTime(date.UtcDateTime);
            return true;
        }

        try
        {
            if (type == typeof(int) && int.TryParse(trimmed, NumberStyles.Integer, inv, out var i))
            {
                value = i;
                return true;
            }

            if (type == typeof(long) && long.TryParse(trimmed, NumberStyles.Integer, inv, out var l))
            {
                value = l;
                return true;
            }

            if (type == typeof(short) && short.TryParse(trimmed, NumberStyles.Integer, inv, out var s))
            {
                value = s;
                return true;
            }

            if (type == typeof(byte) && byte.TryParse(trimmed, NumberStyles.Integer, inv, out var b))
            {
                value = b;
                return true;
            }

            if (type == typeof(decimal) && decimal.TryParse(trimmed, NumberStyles.Number, inv, out var m))
            {
                value = m;
                return true;
            }

            if (type == typeof(double) && double.TryParse(trimmed, NumberStyles.Float, inv, out var dbl))
            {
                value = dbl;
                return true;
            }

            if (type == typeof(float) && float.TryParse(trimmed, NumberStyles.Float, inv, out var f))
            {
                value = f;
                return true;
            }
        }
        catch (OverflowException)
        {
            return false;
        }

        return false;
    }

    /// <summary>Splits a comma separated list of condition values (empty entries are removed, entries are trimmed).</summary>
    /// <param name="text">The list.</param>
    /// <returns>The values (at most 500).</returns>
    public static IReadOnlyList<string> SplitList(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(500).ToList();

    /// <summary>Checks whether a runtime value equals a condition string, coercing the string to the value's type.</summary>
    /// <param name="actual">The runtime value.</param>
    /// <param name="expected">The condition value; null or empty matches empty values.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public static bool ValuesEqual(object? actual, string? expected)
    {
        actual = Normalize(actual);
        if (string.IsNullOrEmpty(expected))
        {
            return IsEmpty(actual);
        }

        if (actual is null)
        {
            return false;
        }

        switch (actual)
        {
            case string s:
                if (string.Equals(s, expected, StringComparison.OrdinalIgnoreCase) || string.Equals(s.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (Guid.TryParse(s, out var sg) && Guid.TryParse(expected, out var eg))
                {
                    return sg == eg;
                }

                if (TryParseDecimal(s, out var sn) && TryParseDecimal(expected, out var en))
                {
                    return sn == en;
                }

                if (TryParseDate(s, out var sd, out _) && TryParseDate(expected, out var ed, out var expectedDateOnly))
                {
                    return expectedDateOnly ? sd.UtcDateTime.Date == ed.UtcDateTime.Date : sd == ed;
                }

                return false;
            case bool flag:
                return TryParseBoolean(expected, out var expectedFlag) && flag == expectedFlag;
            case Guid guid:
                return Guid.TryParse(expected, out var expectedGuid) && guid == expectedGuid;
            case Enum:
                return TryConvert(expected, actual.GetType(), out var enumValue) && Equals(actual, enumValue);
            case DateTimeOffset or DateTime or DateOnly:
                if (!TryParseDate(expected, out var expectedDate, out var dateOnly))
                {
                    return false;
                }

                var actualDate = ToDateTimeOffset(actual);
                return dateOnly || actual is DateOnly ? actualDate.UtcDateTime.Date == expectedDate.UtcDateTime.Date : actualDate == expectedDate;
            case double or float:
                return double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var expectedDouble) && Convert.ToDouble(actual, CultureInfo.InvariantCulture).Equals(expectedDouble);
            case byte or short or int or long or decimal or sbyte or ushort or uint or ulong:
                return TryParseDecimal(expected, out var expectedNumber) && Convert.ToDecimal(actual, CultureInfo.InvariantCulture) == expectedNumber;
            default:
                return string.Equals(Convert.ToString(actual, CultureInfo.InvariantCulture), expected, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Compares a runtime value with a condition string (numbers, dates, enums by numeric value, text). Returns
    /// <see langword="null"/> when the values cannot be ordered (null values, booleans, identifiers, invalid input).
    /// </summary>
    /// <param name="actual">The runtime value.</param>
    /// <param name="expected">The condition value.</param>
    /// <returns>A negative number, zero or a positive number, or <see langword="null"/>.</returns>
    public static int? Compare(object? actual, string? expected)
    {
        actual = Normalize(actual);
        if (actual is null || string.IsNullOrWhiteSpace(expected))
        {
            return null;
        }

        switch (actual)
        {
            case string s:
                if (TryParseDecimal(s, out var sn) && TryParseDecimal(expected, out var en))
                {
                    return sn.CompareTo(en);
                }

                if (TryParseDate(s, out var sd, out _) && TryParseDate(expected, out var ed, out _))
                {
                    return sd.CompareTo(ed);
                }

                return string.Compare(s, expected, StringComparison.OrdinalIgnoreCase);
            case bool or Guid:
                return null;
            case Enum:
                return TryConvert(expected, actual.GetType(), out var enumValue)
                    ? Convert.ToInt64(actual, CultureInfo.InvariantCulture).CompareTo(Convert.ToInt64(enumValue, CultureInfo.InvariantCulture))
                    : null;
            case DateTimeOffset or DateTime or DateOnly:
                return TryParseDate(expected, out var expectedDate, out _) ? ToDateTimeOffset(actual).CompareTo(expectedDate) : null;
            case double or float:
                return double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var expectedDouble)
                    ? Convert.ToDouble(actual, CultureInfo.InvariantCulture).CompareTo(expectedDouble)
                    : null;
            case byte or short or int or long or decimal or sbyte or ushort or uint or ulong:
                return TryParseDecimal(expected, out var expectedNumber) ? Convert.ToDecimal(actual, CultureInfo.InvariantCulture).CompareTo(expectedNumber) : null;
            default:
                return null;
        }
    }

    /// <summary>Converts a runtime value to its invariant text representation (used by text operators).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text or <see langword="null"/>.</returns>
    public static string? ToText(object? value) => Normalize(value) switch
    {
        null => null,
        string s => s,
        DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        var other => other.ToString(),
    };

    /// <summary>Tries to read a runtime value as <see cref="Guid"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="guid">The identifier.</param>
    /// <returns><see langword="true"/> when the value is an identifier.</returns>
    public static bool TryGetGuid(object? value, out Guid guid)
    {
        switch (Normalize(value))
        {
            case Guid g:
                guid = g;
                return true;
            case string s when Guid.TryParse(s, out var parsed):
                guid = parsed;
                return true;
            default:
                guid = Guid.Empty;
                return false;
        }
    }

    private static bool TryParseDecimal(string? text, out decimal value) =>
        decimal.TryParse(text?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    private static DateTimeOffset ToDateTimeOffset(object value) => value switch
    {
        DateTimeOffset d => d,
        DateTime d => new DateTimeOffset(DateTime.SpecifyKind(d, d.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : d.Kind)),
        DateOnly d => new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
        _ => default,
    };
}
