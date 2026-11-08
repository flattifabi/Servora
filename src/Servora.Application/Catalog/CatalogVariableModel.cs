using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>Editable fields of a catalog variable (catalog builder).</summary>
public sealed class CatalogVariableModel
{
    /// <summary>Gets or sets the identifier (<see langword="null"/> for new variables).</summary>
    public Guid? Id { get; set; }

    /// <summary>Gets or sets the technical name (lower snake case).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the input type.</summary>
    public CatalogVariableType Type { get; set; } = CatalogVariableType.Text;

    /// <summary>Gets or sets a value indicating whether an answer is mandatory.</summary>
    public bool Mandatory { get; set; }

    /// <summary>Gets or sets the help text.</summary>
    public string? HelpText { get; set; }

    /// <summary>Gets or sets the choice options, one per line (<c>Label</c> or <c>Label | +8.00 | description</c>).</summary>
    public string? OptionsText { get; set; }

    /// <summary>Gets or sets the order form section (<see langword="null"/> = general section).</summary>
    public Guid? SectionId { get; set; }

    /// <summary>Gets or sets the price impact of a yes / no variable when it is checked.</summary>
    public decimal? PriceDelta { get; set; }

    /// <summary>Gets or sets the default value.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Gets or sets the maximum text length.</summary>
    public int? MaxLength { get; set; }

    /// <summary>Gets or sets the minimum number.</summary>
    public decimal? MinValue { get; set; }

    /// <summary>Gets or sets the maximum number.</summary>
    public decimal? MaxValue { get; set; }

    /// <summary>Converts the model to a definition (for validation and previews).</summary>
    /// <returns>The definition.</returns>
    public CatalogVariableDefinition ToDefinition()
    {
        var choices = CatalogChoiceOption.ParseMany(OptionsText);
        return new CatalogVariableDefinition
        {
            Id = Id ?? Guid.Empty,
            Name = Name.Trim(),
            Label = Label.Trim(),
            Type = Type,
            Mandatory = Mandatory,
            HelpText = string.IsNullOrWhiteSpace(HelpText) ? null : HelpText.Trim(),
            Options = choices.Select(c => c.Label).ToList(),
            Choices = choices,
            SectionId = SectionId,
            PriceDelta = Type == CatalogVariableType.Boolean && PriceDelta is { } delta && delta != 0m ? decimal.Round(delta, 2) : null,
            DefaultValue = string.IsNullOrWhiteSpace(DefaultValue) ? null : DefaultValue.Trim(),
            MaxLength = MaxLength,
            MinValue = MinValue,
            MaxValue = MaxValue,
        };
    }

    /// <summary>Creates a model from a definition.</summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The model.</returns>
    public static CatalogVariableModel From(CatalogVariableDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new CatalogVariableModel
        {
            Id = definition.Id,
            Name = definition.Name,
            Label = definition.Label,
            Type = definition.Type,
            Mandatory = definition.Mandatory,
            HelpText = definition.HelpText,
            OptionsText = string.Join('\n', definition.Choices.Count > 0 ? definition.Choices.Select(c => c.ToLine()) : definition.Options),
            SectionId = definition.SectionId,
            PriceDelta = definition.PriceDelta,
            DefaultValue = definition.DefaultValue,
            MaxLength = definition.MaxLength,
            MinValue = definition.MinValue,
            MaxValue = definition.MaxValue,
        };
    }
}
