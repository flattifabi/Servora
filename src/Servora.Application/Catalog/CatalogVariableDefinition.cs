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

    /// <summary>Gets the option labels of choice variables (the values an answer may take).</summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>Gets the options of choice variables including their price impacts and descriptions.</summary>
    public IReadOnlyList<CatalogChoiceOption> Choices { get; init; } = [];

    /// <summary>Gets the order form section (<see langword="null"/> = general section).</summary>
    public Guid? SectionId { get; init; }

    /// <summary>Gets the price impact of a yes / no variable when it is checked.</summary>
    public decimal? PriceDelta { get; init; }

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
        var choices = CatalogChoiceOption.ParseMany(variable.Options);
        return new CatalogVariableDefinition
        {
            Id = variable.Id,
            Name = variable.Name,
            Label = variable.Label,
            Type = variable.Type,
            Mandatory = variable.Mandatory,
            Order = variable.Order,
            HelpText = variable.HelpText,
            Options = choices.Select(c => c.Label).ToList(),
            Choices = choices,
            SectionId = variable.SectionId,
            PriceDelta = variable.PriceDelta,
            DefaultValue = variable.DefaultValue,
            MaxLength = variable.MaxLength,
            MinValue = variable.MinValue,
            MaxValue = variable.MaxValue,
        };
    }

    /// <summary>
    /// Splits newline separated option lines into a distinct, trimmed list of labels. Price impacts and descriptions
    /// (<c>Label | +8.00 | text</c>) are not part of the label.
    /// </summary>
    /// <param name="options">The options text.</param>
    /// <returns>The option labels.</returns>
    public static IReadOnlyList<string> ParseOptions(string? options) =>
        CatalogChoiceOption.ParseMany(options).Select(o => o.Label).ToList();
}
