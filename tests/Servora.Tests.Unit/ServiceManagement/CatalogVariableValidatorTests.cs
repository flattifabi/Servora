using Servora.Application.Catalog;
using Servora.Domain.Catalog;

namespace Servora.Tests.Unit.ServiceManagement;

/// <summary>Tests for <see cref="CatalogVariableValidator"/>.</summary>
public sealed class CatalogVariableValidatorTests
{
    private static readonly IReadOnlyList<CatalogVariableDefinition> s_form =
    [
        new() { Name = "model", Label = "Model", Type = CatalogVariableType.Choice, Mandatory = true, Options = ["Standard", "Performance"], Order = 10 },
        new() { Name = "count", Label = "Count", Type = CatalogVariableType.Number, MinValue = 1, MaxValue = 5, Order = 20 },
        new() { Name = "needed_by", Label = "Needed by", Type = CatalogVariableType.Date, Order = 30 },
        new() { Name = "terms", Label = "Terms", Type = CatalogVariableType.Boolean, Mandatory = true, Order = 40 },
        new() { Name = "notes", Label = "Notes", Type = CatalogVariableType.Text, MaxLength = 10, Order = 50 },
        new() { Name = "owner", Label = "Owner", Type = CatalogVariableType.UserReference, Order = 60 },
        new() { Name = "os", Label = "OS", Type = CatalogVariableType.Choice, Options = ["Windows", "Linux"], DefaultValue = "Windows", Order = 70 },
    ];

    [Fact]
    public void ValidAnswers_AreNormalized()
    {
        var owner = Guid.NewGuid();
        var result = CatalogVariableValidator.Validate(s_form, new Dictionary<string, string?>
        {
            ["model"] = " performance ",
            ["count"] = "3.0",
            ["needed_by"] = "2026-12-24",
            ["terms"] = "on",
            ["notes"] = "short",
            ["owner"] = owner.ToString("N"),
            ["injected"] = "<script>",
        });

        Assert.True(result.IsValid, result.Summary);
        Assert.Equal("Performance", result.Values["model"]);
        Assert.Equal("3.0", result.Values["count"]);
        Assert.Equal("2026-12-24", result.Values["needed_by"]);
        Assert.Equal("true", result.Values["terms"]);
        Assert.Equal(owner.ToString("D"), result.Values["owner"]);
        Assert.Equal("Windows", result.Values["os"]);
        Assert.False(result.Values.ContainsKey("injected"));
    }

    [Fact]
    public void InvalidAnswers_ProduceFieldErrors()
    {
        var result = CatalogVariableValidator.Validate(s_form, new Dictionary<string, string?>
        {
            ["model"] = "Gaming",
            ["count"] = "9",
            ["needed_by"] = "24.12.2026",
            ["terms"] = "false",
            ["notes"] = "far too long text",
            ["owner"] = "not-a-guid",
            ["os"] = "",
        });

        Assert.False(result.IsValid);
        Assert.Equal(["count", "model", "needed_by", "notes", "owner", "terms"], result.Errors.Keys.Order().ToArray());
        Assert.Null(result.Values["os"]);
        Assert.StartsWith("Please correct", result.Summary);
    }

    [Fact]
    public void MissingMandatoryAnswers_AreReported()
    {
        var result = CatalogVariableValidator.Validate(s_form, new Dictionary<string, string?>());
        Assert.Contains("model", result.Errors.Keys);
        Assert.Contains("terms", result.Errors.Keys);
        Assert.DoesNotContain("count", result.Errors.Keys);
    }

    [Fact]
    public void SingleLineText_RejectsLineBreaks()
    {
        var result = CatalogVariableValidator.Validate([new CatalogVariableDefinition { Name = "a", Label = "A", Type = CatalogVariableType.Text }], new Dictionary<string, string?> { ["a"] = "x\ny" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Definition_Validation()
    {
        Assert.Null(CatalogVariableValidator.ValidateDefinition(new CatalogVariableModel { Name = "cost_center", Label = "Cost center" }, ["other"]));
        Assert.NotNull(CatalogVariableValidator.ValidateDefinition(new CatalogVariableModel { Name = "Cost Center", Label = "Cost center" }, []));
        Assert.NotNull(CatalogVariableValidator.ValidateDefinition(new CatalogVariableModel { Name = "dup", Label = "Duplicate" }, ["dup"]));
        Assert.NotNull(CatalogVariableValidator.ValidateDefinition(new CatalogVariableModel { Name = "size", Label = "Size", Type = CatalogVariableType.Choice }, []));
        Assert.NotNull(CatalogVariableValidator.ValidateDefinition(new CatalogVariableModel { Name = "size", Label = "Size", Type = CatalogVariableType.Choice, OptionsText = "S\nM", DefaultValue = "XL" }, []));
        Assert.Null(CatalogVariableValidator.ValidateDefinition(new CatalogVariableModel { Name = "size", Label = "Size", Type = CatalogVariableType.Choice, OptionsText = "S\r\nM\n\nM", DefaultValue = "m" }, []));
        Assert.NotNull(CatalogVariableValidator.ValidateDefinition(new CatalogVariableModel { Name = "n", Label = "N", Type = CatalogVariableType.Number, MinValue = 5, MaxValue = 1 }, []));
    }
}
