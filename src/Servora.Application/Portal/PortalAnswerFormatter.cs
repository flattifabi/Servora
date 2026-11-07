using System.Globalization;
using System.Text.Json;
using Servora.Application.Catalog;
using Servora.Domain.Catalog;

namespace Servora.Application.Portal;

/// <summary>Turns the stored order form answers of a requested item into display answers (approver view).</summary>
internal static class PortalAnswerFormatter
{
    /// <summary>Parses the answers JSON of a requested item.</summary>
    /// <param name="json">The JSON object (variable name → value).</param>
    /// <returns>The answers; empty when the JSON is missing or malformed.</returns>
    public static Dictionary<string, string?> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new Dictionary<string, string?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>();
        }
    }

    /// <summary>Builds the display answers in the order of the variable definitions.</summary>
    /// <param name="definitions">The variable definitions of the catalog item.</param>
    /// <param name="answers">The stored answers.</param>
    /// <param name="users">Display names of referenced users.</param>
    /// <returns>The display answers (answers of deleted variables are appended with their technical name).</returns>
    public static IReadOnlyList<VariableAnswer> Build(IReadOnlyList<CatalogVariable> definitions, IReadOnlyDictionary<string, string?> answers, IReadOnlyDictionary<Guid, string> users)
    {
        var result = new List<VariableAnswer>();
        foreach (var definition in definitions)
        {
            answers.TryGetValue(definition.Name, out var value);
            result.Add(new VariableAnswer(definition.Name, definition.Label, definition.Type, Display(definition.Type, value, users)));
        }

        foreach (var (name, value) in answers.Where(a => definitions.All(d => d.Name != a.Key)))
        {
            result.Add(new VariableAnswer(name, name, CatalogVariableType.Text, value));
        }

        return result;
    }

    private static string? Display(CatalogVariableType type, string? value, IReadOnlyDictionary<Guid, string> users)
    {
        if (value is null)
        {
            return null;
        }

        return type switch
        {
            CatalogVariableType.Boolean => value == "true" ? "Yes" : "No",
            CatalogVariableType.UserReference => Guid.TryParse(value, out var id) && users.TryGetValue(id, out var name) ? name : "Unknown user",
            CatalogVariableType.Date => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
                : value,
            _ => value,
        };
    }
}
