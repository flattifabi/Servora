using Servora.Application.Cmdb;
using Servora.Domain.Cmdb;

namespace Servora.Tests.Unit.KnowledgeCmdb;

/// <summary>Tests for <see cref="CiAttributeSchema"/> and <see cref="CiClassHierarchy"/>.</summary>
public sealed class CiAttributeSchemaTests
{
    private static readonly List<CiAttributeDefinition> s_definitions =
    [
        new() { Name = "os", Label = "OS", Type = CiAttributeType.Choice, Choices = ["Linux", "Windows"], Required = true },
        new() { Name = "ram_gb", Label = "Memory", Type = CiAttributeType.Number },
        new() { Name = "virtual", Label = "Virtual", Type = CiAttributeType.Boolean },
        new() { Name = "eol", Label = "End of life", Type = CiAttributeType.Date },
    ];

    [Fact]
    public void Definitions_RoundTrip()
    {
        var json = CiAttributeSchema.SerializeDefinitions(s_definitions);
        var parsed = CiAttributeSchema.ParseDefinitions(json);

        Assert.Equal(4, parsed.Count);
        Assert.Equal(CiAttributeType.Choice, parsed[0].Type);
        Assert.Equal(["Linux", "Windows"], parsed[0].Choices);
    }

    [Theory]
    [InlineData("Bad Name")]
    [InlineData("1abc")]
    [InlineData("")]
    public void ValidateDefinitions_RejectsInvalidNames(string name)
    {
        Assert.NotNull(CiAttributeSchema.ValidateDefinitions([new CiAttributeDefinition { Name = name, Label = "x" }]));
    }

    [Fact]
    public void ValidateDefinitions_RejectsDuplicatesAndEmptyChoices()
    {
        Assert.NotNull(CiAttributeSchema.ValidateDefinitions([new() { Name = "a", Label = "A" }, new() { Name = "a", Label = "B" }]));
        Assert.NotNull(CiAttributeSchema.ValidateDefinitions([new() { Name = "a", Label = "A", Type = CiAttributeType.Choice }]));
        Assert.Null(CiAttributeSchema.ValidateDefinitions(s_definitions));
    }

    [Fact]
    public void ApplyValues_NormalisesAndPreservesForeignFields()
    {
        var values = new Dictionary<string, string?> { ["os"] = "linux", ["ram_gb"] = "32.5", ["virtual"] = "True", ["eol"] = "2030-01-31" };

        var error = CiAttributeSchema.ApplyValues(s_definitions, values, "{\"custom_x\":\"keep\"}", out var json);
        var parsed = CiAttributeSchema.ParseValues(json);

        Assert.Null(error);
        Assert.Equal("Linux", parsed["os"]);
        Assert.Equal("32.5", parsed["ram_gb"]);
        Assert.Equal("true", parsed["virtual"]);
        Assert.Equal("2030-01-31", parsed["eol"]);
        Assert.Equal("keep", parsed["custom_x"]);
    }

    [Theory]
    [InlineData("os", "Solaris")]
    [InlineData("ram_gb", "lots")]
    [InlineData("virtual", "maybe")]
    [InlineData("eol", "31.01.2030")]
    public void ApplyValues_RejectsInvalidValues(string field, string value)
    {
        var values = new Dictionary<string, string?> { ["os"] = "Linux", [field] = value };

        Assert.NotNull(CiAttributeSchema.ApplyValues(s_definitions, values, null, out _));
    }

    [Fact]
    public void ApplyValues_EnforcesRequired()
    {
        Assert.Equal("OS is required.", CiAttributeSchema.ApplyValues(s_definitions, new Dictionary<string, string?>(), null, out _));
    }

    [Fact]
    public void Hierarchy_InheritsAttributesAndFindsDescendants()
    {
        var classes = new List<CiClass>
        {
            new() { Key = "server", Label = "Server", AttributesJson = CiAttributeSchema.SerializeDefinitions([new() { Name = "os", Label = "OS" }]) },
            new() { Key = "virtual_machine", Label = "VM", ParentKey = "server", AttributesJson = CiAttributeSchema.SerializeDefinitions([new() { Name = "hypervisor", Label = "Hypervisor" }]) },
            new() { Key = "container_host", Label = "Container host", ParentKey = "virtual_machine" },
            new() { Key = "database", Label = "Database" },
        };

        Assert.Equal(["os", "hypervisor"], CiClassHierarchy.EffectiveAttributes(classes, "container_host").Select(a => a.Name));
        Assert.Equal(["Server", "VM", "Container host"], CiClassHierarchy.Chain(classes, "container_host").Select(c => c.Label));
        Assert.Equal(new HashSet<string> { "server", "virtual_machine", "container_host" }, CiClassHierarchy.SelfAndDescendants(classes, "server").ToHashSet());
        Assert.True(CiClassHierarchy.WouldCreateCycle(classes, "server", "container_host"));
        Assert.False(CiClassHierarchy.WouldCreateCycle(classes, "database", "server"));
    }
}
