using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>Read model of a catalog variable used to render order forms and to validate answers.</summary>
public sealed record CatalogVariableDefinition
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the technical name (answer key).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the label.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Gets the input type.</summary>
    public CatalogVariableType Type { get; init; }

    /// <summary>Gets a value indicating whether an answer is mandatory.</summary>
    public bool Mandatory { get; init; }

    /// <summary>Gets the display order.</summary>
    public int Order { get; init; }

    /// <summary>Gets the help text.</summary>
    public string? HelpText { get; init; }

    /// <summary>Gets the options of choice variables.</summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>Gets the default value.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>Gets the maximum text length.</summary>
    public int? MaxLength { get; init; }

    /// <summary>Gets the minimum number.</summary>
    public decimal? MinValue { get; init; }

    /// <summary>Gets the maximum number.</summary>
    public decimal? MaxValue { get; init; }

    /// <summary>Creates a definition from an entity.</summary>
    /// <param name="variable">The entity.</param>
    /// <returns>The definition.</returns>
    public static CatalogVariableDefinition From(CatalogVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        return new CatalogVariableDefinition
        {
            Id = variable.Id,
            Name = variable.Name,
            Label = variable.Label,
            Type = variable.Type,
            Mandatory = variable.Mandatory,
            Order = variable.Order,
            HelpText = variable.HelpText,
            Options = ParseOptions(variable.Options),
            DefaultValue = variable.DefaultValue,
            MaxLength = variable.MaxLength,
            MinValue = variable.MinValue,
            MaxValue = variable.MaxValue,
        };
    }

    /// <summary>Splits newline separated options into a distinct, trimmed list.</summary>
    /// <param name="options">The options text.</param>
    /// <returns>The options.</returns>
    public static IReadOnlyList<string> ParseOptions(string? options) =>
        string.IsNullOrWhiteSpace(options)
            ? []
            : options.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(o => o.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
}
