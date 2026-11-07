using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Servora.Application.Common;

namespace Servora.Application.Configuration;

/// <summary>
/// Validates, normalizes and converts setting values according to their <see cref="SettingDefinition"/>. Values are
/// always stored in a canonical, culture-invariant string form (for example <c>true</c>, <c>42</c>, <c>#7c6cff</c>).
/// </summary>
public static partial class SettingValueParser
{
    /// <summary>Validates and normalizes a raw input value.</summary>
    /// <param name="definition">The setting definition.</param>
    /// <param name="input">The raw input; <see langword="null"/> or white space means "empty".</param>
    /// <returns>The normalized value (empty string for an empty value) or a validation error.</returns>
    public static Result<string> Normalize(SettingDefinition definition, string? input)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var value = definition.ValueType == SettingValueType.MultilineText
            ? (input ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Trim()
            : (input ?? string.Empty).Trim();

        if (value.Length == 0)
        {
            if (definition.Required)
            {
                return Result<string>.Invalid($"{definition.Label} is required.");
            }

            // Booleans and integers always have a concrete value; an empty input falls back to the default.
            return definition.ValueType is SettingValueType.Boolean or SettingValueType.Integer
                ? Result<string>.Success(definition.DefaultValue ?? string.Empty)
                : Result<string>.Success(string.Empty);
        }

        if (value.Length > definition.EffectiveMaxLength)
        {
            return Result<string>.Invalid($"{definition.Label} must not exceed {definition.EffectiveMaxLength} characters.");
        }

        if (definition.ValueType != SettingValueType.MultilineText && value.Any(char.IsControl))
        {
            return Result<string>.Invalid($"{definition.Label} must not contain control characters.");
        }

        return definition.ValueType switch
        {
            SettingValueType.String or SettingValueType.MultilineText => Result<string>.Success(value),
            SettingValueType.Integer => NormalizeInteger(definition, value),
            SettingValueType.Boolean => NormalizeBoolean(definition, value),
            SettingValueType.Color => NormalizeColor(definition, value),
            SettingValueType.Choice => NormalizeChoice(definition, value),
            SettingValueType.Email => MailAddress.TryCreate(value, out var address) && string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase)
                ? Result<string>.Success(address.Address)
                : Result<string>.Invalid($"{definition.Label} must be a valid e-mail address."),
            SettingValueType.Url => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && string.IsNullOrEmpty(uri.UserInfo)
                ? Result<string>.Success(value.TrimEnd('/'))
                : Result<string>.Invalid($"{definition.Label} must be an absolute http(s) URL without credentials."),
            SettingValueType.TimeZone => TimeZoneInfo.TryFindSystemTimeZoneById(value, out _)
                ? Result<string>.Success(value)
                : Result<string>.Invalid($"'{value}' is not a time zone known to this server."),
            _ => Result<string>.Invalid($"Unsupported setting type {definition.ValueType}."),
        };
    }

    /// <summary>Converts a stored (normalized) value into a typed value.</summary>
    /// <typeparam name="T">The target type: <see cref="string"/>, <see cref="bool"/>, <see cref="int"/>, <see cref="long"/> or an enum.</typeparam>
    /// <param name="value">The stored value.</param>
    /// <param name="result">The converted value.</param>
    /// <returns><see langword="true"/> when the conversion succeeded.</returns>
    public static bool TryConvert<T>(string? value, out T result)
    {
        result = default!;
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (target == typeof(string))
        {
            result = (T)(object)(value ?? string.Empty);
            return true;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        object? converted = null;
        if (target == typeof(bool) && TryParseBoolean(value, out var b))
        {
            converted = b;
        }
        else if (target == typeof(int) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
        {
            converted = i;
        }
        else if (target == typeof(long) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            converted = l;
        }
        else if (target.IsEnum && Enum.TryParse(target, value, ignoreCase: true, out var e) && Enum.IsDefined(target, e!))
        {
            converted = e;
        }

        if (converted is null)
        {
            return false;
        }

        result = (T)converted;
        return true;
    }

    /// <summary>Checks whether a key is a syntactically valid setting key (lower case, dot separated).</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValidKey(string? key) => key is not null && key.Length <= 200 && KeyPattern().IsMatch(key);

    /// <summary>Checks whether a key is a valid custom feature flag key (<c>features.&lt;name&gt;</c>).</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValidFeatureKey(string? key) => key is not null && key.Length <= 100 && FeatureKeyPattern().IsMatch(key);

    /// <summary>Parses the boolean spellings accepted by the settings UI and API.</summary>
    /// <param name="value">The input.</param>
    /// <param name="result">The parsed value.</param>
    /// <returns><see langword="true"/> when the input is a recognized boolean.</returns>
    public static bool TryParseBoolean(string? value, out bool result)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "true" or "1" or "yes" or "on":
                result = true;
                return true;
            case "false" or "0" or "no" or "off":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static Result<string> NormalizeInteger(SettingDefinition definition, string value)
    {
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
        {
            return Result<string>.Invalid($"{definition.Label} must be a whole number.");
        }

        if (definition.Min is { } min && number < min)
        {
            return Result<string>.Invalid($"{definition.Label} must be at least {min}.");
        }

        if (definition.Max is { } max && number > max)
        {
            return Result<string>.Invalid($"{definition.Label} must be at most {max}.");
        }

        return Result<string>.Success(number.ToString(CultureInfo.InvariantCulture));
    }

    private static Result<string> NormalizeBoolean(SettingDefinition definition, string value) =>
        TryParseBoolean(value, out var flag)
            ? Result<string>.Success(flag ? "true" : "false")
            : Result<string>.Invalid($"{definition.Label} must be true or false.");

    private static Result<string> NormalizeColor(SettingDefinition definition, string value)
    {
        var match = ColorPattern().Match(value);
        if (!match.Success)
        {
            return Result<string>.Invalid($"{definition.Label} must be a hex color such as #7c6cff.");
        }

        var hex = match.Groups[1].Value.ToLowerInvariant();
        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        return Result<string>.Success("#" + hex);
    }

    private static Result<string> NormalizeChoice(SettingDefinition definition, string value)
    {
        var choice = definition.Choices.FirstOrDefault(c => string.Equals(c, value, StringComparison.OrdinalIgnoreCase));
        return choice is not null
            ? Result<string>.Success(choice)
            : Result<string>.Invalid($"{definition.Label} must be one of: {string.Join(", ", definition.Choices)}.");
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*(\\.[a-z0-9_]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^features\\.[a-z][a-z0-9_]{1,60}$", RegexOptions.CultureInvariant)]
    private static partial Regex FeatureKeyPattern();

    [GeneratedRegex("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{3})$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
}
