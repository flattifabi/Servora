using System.Globalization;
using System.Text.RegularExpressions;
using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>
/// Validates and normalizes order form answers against catalog variable definitions, and validates variable definitions
/// in the catalog builder. Answers for unknown variables are dropped, so requested items only ever store data the
/// catalog item asked for.
/// </summary>
public static partial class CatalogVariableValidator
{
    /// <summary>Default maximum length of single line text answers.</summary>
    public const int DefaultTextLength = 1000;

    /// <summary>Default maximum length of multi line text answers.</summary>
    public const int DefaultMultilineLength = 4000;

    /// <summary>Upper bound of any configurable maximum length.</summary>
    public const int MaxConfigurableLength = 4000;

    /// <summary>Maximum number of options of a choice variable.</summary>
    public const int MaxOptions = 100;

    /// <summary>Maximum number of variables of one catalog item.</summary>
    public const int MaxVariablesPerItem = 50;

    private static readonly string[] s_trueValues = ["true", "yes", "on", "1"];
    private static readonly string[] s_falseValues = ["false", "no", "off", "0"];

    /// <summary>Validates the answers of an order form.</summary>
    /// <param name="definitions">The variable definitions of the catalog item.</param>
    /// <param name="answers">The raw answers keyed by variable name. Missing answers fall back to the default value.</param>
    /// <returns>The validation result with normalized values.</returns>
    public static VariableValidationResult Validate(IReadOnlyList<CatalogVariableDefinition> definitions, IReadOnlyDictionary<string, string?>? answers)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        answers ??= new Dictionary<string, string?>();
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var definition in definitions.OrderBy(d => d.Order))
        {
            var raw = answers.TryGetValue(definition.Name, out var provided) ? provided : definition.DefaultValue;
            var text = raw?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                if (definition.Type == CatalogVariableType.Boolean)
                {
                    if (definition.Mandatory)
                    {
                        errors[definition.Name] = $"{definition.Label} must be confirmed.";
                    }
                    else
                    {
                        values[definition.Name] = "false";
                    }
                }
                else if (definition.Mandatory)
                {
                    errors[definition.Name] = $"{definition.Label} is required.";
                }
                else
                {
                    values[definition.Name] = null;
                }

                continue;
            }

            var (value, error) = Normalize(definition, text);
            if (error is not null)
            {
                errors[definition.Name] = error;
            }
            else
            {
                values[definition.Name] = value;
            }
        }

        return new VariableValidationResult(errors, values);
    }

    /// <summary>Validates a variable definition of the catalog builder.</summary>
    /// <param name="model">The variable.</param>
    /// <param name="existingNames">Names of the item's other variables (for the uniqueness check).</param>
    /// <returns><see langword="null"/> when valid; otherwise a user-safe message.</returns>
    public static string? ValidateDefinition(CatalogVariableModel model, IEnumerable<string> existingNames)
    {
        ArgumentNullException.ThrowIfNull(model);
        var name = model.Name?.Trim() ?? string.Empty;
        if (!NamePattern().IsMatch(name))
        {
            return "The name must start with a lowercase letter and contain only lowercase letters, digits and underscores (max. 64).";
        }

        if (existingNames.Contains(name, StringComparer.Ordinal))
        {
            return $"Another variable of this item is already named \"{name}\".";
        }

        if (string.IsNullOrWhiteSpace(model.Label) || model.Label.Trim().Length > 200)
        {
            return "The label is required (max. 200 characters).";
        }

        if (!Enum.IsDefined(model.Type))
        {
            return "The variable type is invalid.";
        }

        if (model.HelpText is { Length: > 1000 } || model.DefaultValue is { Length: > 1000 })
        {
            return "Help text and default value must not exceed 1000 characters.";
        }

        if (model.MaxLength is { } maxLength && (maxLength < 1 || maxLength > MaxConfigurableLength))
        {
            return $"The maximum length must be between 1 and {MaxConfigurableLength}.";
        }

        if (model.MinValue is { } min && model.MaxValue is { } max && min > max)
        {
            return "The minimum must not be greater than the maximum.";
        }

        var definition = model.ToDefinition();
        if (model.Type == CatalogVariableType.Choice)
        {
            if (definition.Options.Count == 0)
            {
                return "A choice variable needs at least one option.";
            }

            if (definition.Options.Count > MaxOptions || definition.Options.Any(o => o.Length > 200))
            {
                return $"A choice variable can have at most {MaxOptions} options of up to 200 characters.";
            }
        }

        if (definition.DefaultValue is not null)
        {
            var check = Validate([definition with { Mandatory = false }], new Dictionary<string, string?> { [definition.Name] = definition.DefaultValue });
            if (!check.IsValid)
            {
                return "The default value is not valid for this variable: " + check.Errors.Values.First();
            }
        }

        return null;
    }

    private static (string? Value, string? Error) Normalize(CatalogVariableDefinition definition, string text)
    {
        switch (definition.Type)
        {
            case CatalogVariableType.Text:
                if (text.Contains('\n') || text.Contains('\r'))
                {
                    return (null, $"{definition.Label} must be a single line.");
                }

                return text.Length > (definition.MaxLength ?? DefaultTextLength)
                    ? (null, $"{definition.Label} must not exceed {definition.MaxLength ?? DefaultTextLength} characters.")
                    : (text, null);

            case CatalogVariableType.MultilineText:
                return text.Length > (definition.MaxLength ?? DefaultMultilineLength)
                    ? (null, $"{definition.Label} must not exceed {definition.MaxLength ?? DefaultMultilineLength} characters.")
                    : (text, null);

            case CatalogVariableType.Number:
                if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    return (null, $"{definition.Label} must be a number.");
                }

                if (definition.MinValue is { } min && number < min)
                {
                    return (null, $"{definition.Label} must be at least {min.ToString(CultureInfo.InvariantCulture)}.");
                }

                if (definition.MaxValue is { } max && number > max)
                {
                    return (null, $"{definition.Label} must be at most {max.ToString(CultureInfo.InvariantCulture)}.");
                }

                return (number.ToString(CultureInfo.InvariantCulture), null);

            case CatalogVariableType.Choice:
                var option = definition.Options.FirstOrDefault(o => string.Equals(o, text, StringComparison.OrdinalIgnoreCase));
                return option is null ? (null, $"{definition.Label}: select one of the offered options.") : (option, null);

            case CatalogVariableType.Boolean:
                var lower = text.ToLowerInvariant();
                if (s_trueValues.Contains(lower))
                {
                    return ("true", null);
                }

                if (s_falseValues.Contains(lower))
                {
                    return definition.Mandatory ? (null, $"{definition.Label} must be confirmed.") : ("false", null);
                }

                return (null, $"{definition.Label} must be yes or no.");

            case CatalogVariableType.Date:
                return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    ? (date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null)
                    : (null, $"{definition.Label} must be a valid date (YYYY-MM-DD).");

            case CatalogVariableType.UserReference:
                return Guid.TryParse(text, out var userId) && userId != Guid.Empty
                    ? (userId.ToString("D"), null)
                    : (null, $"{definition.Label}: select a valid user.");

            default:
                return (null, $"{definition.Label} has an unsupported type.");
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$")]
    private static partial Regex NamePattern();
}
