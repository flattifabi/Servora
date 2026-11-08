using Servora.Application.Catalog;
using Servora.Domain.Catalog;

namespace Servora.Tests.Unit.Catalog;

/// <summary>Tests of the option line format with price impacts and its backwards compatibility.</summary>
public sealed class CatalogChoiceOptionTests
{
    [Fact]
    public void Parse_PlainLabel_HasNoPriceImpact()
    {
        var option = CatalogChoiceOption.Parse("  Windows 11  ");
        Assert.Equal(new CatalogChoiceOption("Windows 11"), option);
    }

    [Theory]
    [InlineData("32 GB RAM | +8.00", "32 GB RAM", 8.00, null)]
    [InlineData("32 GB RAM|8", "32 GB RAM", 8.00, null)]
    [InlineData("Basic | -2.50 | Cheaper option", "Basic", -2.50, "Cheaper option")]
    [InlineData("Dock | € 5.00 | One cable | for all", "Dock", 5.00, "One cable | for all")]
    [InlineData("16 GB RAM | 0 | Included in the base price", "16 GB RAM", 0, "Included in the base price")]
    public void Parse_PricedLine_SplitsLabelPriceAndDescription(string line, string label, double price, string? description)
    {
        var option = CatalogChoiceOption.Parse(line)!;
        Assert.Equal(label, option.Label);
        Assert.Equal((decimal)price, option.PriceDelta);
        Assert.Equal(description, option.Description);
    }

    [Theory]
    [InlineData("Plan A | Plan B")]
    [InlineData("Read | Write")]
    public void Parse_LegacyLabelWithPipe_IsKeptAsPlainLabel(string line)
    {
        var option = CatalogChoiceOption.Parse(line)!;
        Assert.Equal(line, option.Label);
        Assert.Equal(0m, option.PriceDelta);
    }

    [Fact]
    public void ParseMany_IgnoresEmptyLinesAndDuplicateLabels()
    {
        var options = CatalogChoiceOption.ParseMany("A\n\n a \nB | +1\nb | +2\r\n");
        Assert.Equal(["A", "B"], options.Select(o => o.Label));
        Assert.Equal(1m, options[1].PriceDelta);
    }

    [Fact]
    public void ToLine_RoundTrips()
    {
        foreach (var option in new[] { new CatalogChoiceOption("Plain"), new CatalogChoiceOption("Up", 8m), new CatalogChoiceOption("Down", -2.5m, "Note") })
        {
            Assert.Equal(option, CatalogChoiceOption.Parse(option.ToLine()));
        }

        Assert.Equal("Plain", new CatalogChoiceOption("Plain").ToLine());
    }

    [Fact]
    public void Definition_FromLegacyOptions_ExposesLabelsUnchanged()
    {
        var variable = new CatalogVariable { Name = "os", Label = "OS", Type = CatalogVariableType.Choice, Options = "Windows 11\nUbuntu 24.04 LTS" };
        var definition = CatalogVariableDefinition.From(variable);
        Assert.Equal(["Windows 11", "Ubuntu 24.04 LTS"], definition.Options);
        Assert.All(definition.Choices, c => Assert.Equal(0m, c.PriceDelta));
        Assert.Equal(["Windows 11", "Ubuntu 24.04 LTS"], CatalogVariableDefinition.ParseOptions(variable.Options));
    }

    [Fact]
    public void Validator_AcceptsTheLabelOfAPricedOption_AndStoresOnlyTheLabel()
    {
        var definition = CatalogVariableDefinition.From(new CatalogVariable
        {
            Name = "memory", Label = "Memory", Type = CatalogVariableType.Choice, Mandatory = true, Options = "16 GB RAM | 0\n32 GB RAM | +8.00 | Faster",
        });
        var result = CatalogVariableValidator.Validate([definition], new Dictionary<string, string?> { ["memory"] = "32 gb ram" });
        Assert.True(result.IsValid);
        Assert.Equal("32 GB RAM", result.Values["memory"]);

        var invalid = CatalogVariableValidator.Validate([definition], new Dictionary<string, string?> { ["memory"] = "32 GB RAM | +8.00 | Faster" });
        Assert.False(invalid.IsValid);
    }

    [Fact]
    public void ValidateDefinition_RejectsOutOfRangePriceImpacts()
    {
        var model = new CatalogVariableModel { Name = "memory", Label = "Memory", Type = CatalogVariableType.Choice, OptionsText = "Huge | +2000000" };
        Assert.NotNull(CatalogVariableValidator.ValidateDefinition(model, []));

        var accessory = new CatalogVariableModel { Name = "dock", Label = "Dock", Type = CatalogVariableType.Boolean, PriceDelta = 5m };
        Assert.Null(CatalogVariableValidator.ValidateDefinition(accessory, []));
        Assert.Equal(5m, accessory.ToDefinition().PriceDelta);

        var text = new CatalogVariableModel { Name = "note", Label = "Note", Type = CatalogVariableType.Text, PriceDelta = 5m };
        Assert.Null(text.ToDefinition().PriceDelta);
    }

    [Fact]
    public void Model_FromDefinition_KeepsPricedOptionLines()
    {
        var model = new CatalogVariableModel { Name = "memory", Label = "Memory", Type = CatalogVariableType.Choice, OptionsText = "16 GB RAM\n32 GB RAM | +8.00 | Faster" };
        var copy = CatalogVariableModel.From(model.ToDefinition());
        Assert.Equal("16 GB RAM\n32 GB RAM | +8.00 | Faster", copy.OptionsText);
    }
}
