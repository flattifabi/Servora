using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using Servora.Application.Conditions;

namespace Servora.Application.Tables;

/// <summary>
/// Converts and validates incoming field values (invariant strings, JSON elements, numbers, booleans) into typed CLR
/// values for a field. Custom fields use canonical types: <see cref="string"/>, <see cref="long"/>, <see cref="decimal"/>,
/// <see cref="bool"/>, <see cref="DateTimeOffset"/>, <see cref="DateOnly"/> and <see cref="Guid"/>.
/// </summary>
public static class RecordValueConverter
{
    /// <summary>Converts a raw value for a field.</summary>
    /// <param name="raw">The raw value.</param>
    /// <param name="field">The field definition.</param>
    /// <param name="clrType">The CLR property type for typed fields, or <see langword="null"/> for custom fields.</param>
    /// <param name="value">The converted value.</param>
    /// <param name="error">A user-safe error message when the conversion failed.</param>
    /// <returns><see langword="true"/> when the value is valid.</returns>
    public static bool TryConvert(object? raw, FieldDefinition field, Type? clrType, out object? value, out string? error)
    {
        value = null;
        error = null;
        raw = ConditionValueConverter.Normalize(raw);
        var target = clrType is null ? null : Nullable.GetUnderlyingType(clrType) ?? clrType;
        var isTextField = field.Type is FieldType.String or FieldType.Text;

        if (raw is null || (raw is string blank && string.IsNullOrWhiteSpace(blank) && !isTextField))
        {
            if (clrType is { IsValueType: true } && Nullable.GetUnderlyingType(clrType) is null)
            {
                error = $"'{field.Label}' requires a value.";
                return false;
            }

            return true;
        }

        var inv = CultureInfo.InvariantCulture;
        switch (field.Type)
        {
            case FieldType.String or FieldType.Text:
                value = ConditionValueConverter.ToText(raw);
                return true;
            case FieldType.Email:
                var email = ConditionValueConverter.ToText(raw)!.Trim();
                if (email.Length > 254 || !MailAddress.TryCreate(email, out var address) || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"'{field.Label}' must be a valid e-mail address.";
                    return false;
                }

                value = email;
                return true;
            case FieldType.Url:
                var url = ConditionValueConverter.ToText(raw)!.Trim();
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    error = $"'{field.Label}' must be an absolute http or https URL.";
                    return false;
                }

                value = url;
                return true;
            case FieldType.Json:
                var json = ConditionValueConverter.ToText(raw)!;
                try
                {
                    using var _ = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
                }
                catch (JsonException)
                {
                    error = $"'{field.Label}' must contain valid JSON.";
                    return false;
                }

                value = json;
                return true;
            case FieldType.Integer:
                if (!TryGetDecimal(raw, out var whole) || decimal.Truncate(whole) != whole)
                {
                    error = $"'{field.Label}' must be a whole number.";
                    return false;
                }

                return ConvertNumber(whole, target ?? typeof(long), field, out value, out error);
            case FieldType.Decimal:
                if (!TryGetDecimal(raw, out var number))
                {
                    error = $"'{field.Label}' must be a number.";
                    return false;
                }

                return ConvertNumber(number, target ?? typeof(decimal), field, out value, out error);
            case FieldType.Boolean:
                if (raw is bool flag || ConditionValueConverter.TryParseBoolean(ConditionValueConverter.ToText(raw), out flag))
                {
                    value = flag;
                    return true;
                }

                error = $"'{field.Label}' must be true or false.";
                return false;
            case FieldType.DateTime:
                DateTimeOffset moment;
                if (raw is DateTimeOffset dto)
                {
                    moment = dto.ToUniversalTime();
                }
                else if (raw is DateTime dt)
                {
                    moment = new DateTimeOffset(DateTime.SpecifyKind(dt, dt.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : dt.Kind)).ToUniversalTime();
                }
                else if (!ConditionValueConverter.TryParseDate(ConditionValueConverter.ToText(raw), out moment, out _))
                {
                    error = $"'{field.Label}' must be an ISO 8601 date and time.";
                    return false;
                }

                value = target == typeof(DateTime) ? moment.UtcDateTime : moment;
                return true;
            case FieldType.Date:
                DateOnly date;
                if (raw is DateOnly d)
                {
                    date = d;
                }
                else if (ConditionValueConverter.TryParseDate(ConditionValueConverter.ToText(raw), out var parsed, out _))
                {
                    date = DateOnly.FromDateTime(parsed.UtcDateTime);
                }
                else
                {
                    error = $"'{field.Label}' must be a date (yyyy-MM-dd).";
                    return false;
                }

                value = target == typeof(DateTimeOffset) ? new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : date;
                return true;
            case FieldType.Choice:
                return ConvertChoice(raw, field, target, out value, out error);
            case FieldType.Reference or FieldType.Guid:
                if (ConditionValueConverter.TryGetGuid(raw, out var id) && id != Guid.Empty)
                {
                    value = id;
                    return true;
                }

                error = $"'{field.Label}' must be a record identifier.";
                return false;
            default:
                value = ConditionValueConverter.ToText(raw);
                return true;
        }

        static bool TryGetDecimal(object raw, out decimal result)
        {
            switch (raw)
            {
                case decimal m:
                    result = m;
                    return true;
                case double or float:
                    var dbl = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                    if (double.IsFinite(dbl) && Math.Abs(dbl) < 7.9e27)
                    {
                        result = (decimal)dbl;
                        return true;
                    }

                    result = 0;
                    return false;
                case byte or short or int or long or sbyte or ushort or uint:
                    result = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
                    return true;
                case string s:
                    return decimal.TryParse(s.Trim(), NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out result);
                default:
                    result = 0;
                    return false;
            }
        }
    }

    private static bool ConvertNumber(decimal number, Type target, FieldDefinition field, out object? value, out string? error)
    {
        value = null;
        error = null;
        try
        {
            value = target == typeof(int) ? decimal.ToInt32(number)
                : target == typeof(long) ? decimal.ToInt64(number)
                : target == typeof(short) ? decimal.ToInt16(number)
                : target == typeof(byte) ? decimal.ToByte(number)
                : target == typeof(double) ? decimal.ToDouble(number)
                : target == typeof(float) ? decimal.ToSingle(number)
                : number;
            return true;
        }
        catch (OverflowException)
        {
            error = $"'{field.Label}' is out of range.";
            return false;
        }
    }

    private static bool ConvertChoice(object raw, FieldDefinition field, Type? target, out object? value, out string? error)
    {
        value = null;
        error = null;
        var text = ConditionValueConverter.ToText(raw)!.Trim();
        if (target is { IsEnum: true })
        {
            if (raw is Enum && raw.GetType() == target)
            {
                value = raw;
                return true;
            }

            if (ConditionValueConverter.TryConvert(text, target, out var enumValue) && Enum.IsDefined(target, enumValue!))
            {
                value = enumValue;
                return true;
            }

            error = $"'{field.Label}' must be one of: {string.Join(", ", field.Choices.Select(c => c.Label))}.";
            return false;
        }

        if (field.Choices.Count == 0 && target is not null)
        {
            return ConditionValueConverter.TryConvert(text, target, out value) || Fail(field, out error);
        }

        var choice = field.Choices.FirstOrDefault(c => string.Equals(c.Value, text, StringComparison.OrdinalIgnoreCase))
            ?? field.Choices.FirstOrDefault(c => string.Equals(c.Label, text, StringComparison.OrdinalIgnoreCase));
        if (choice is null)
        {
            error = $"'{field.Label}' must be one of: {string.Join(", ", field.Choices.Select(c => c.Label))}.";
            return false;
        }

        if (target is not null && target != typeof(string))
        {
            return ConditionValueConverter.TryConvert(choice.Value, target, out value) || Fail(field, out error);
        }

        value = choice.Value;
        return true;
    }

    private static bool Fail(FieldDefinition field, out string? error)
    {
        error = $"'{field.Label}' has an invalid value.";
        return false;
    }
}
