using System.Text.Json.Nodes;
using Servora.Application.Automation.Templates;

namespace Servora.Tests.Unit.Automation;

/// <summary>Tests of the safe data pill resolver <see cref="FlowTemplate"/>.</summary>
public sealed class FlowTemplateTests
{
    private static readonly JsonNode s_data = JsonNode.Parse("""
        {
          "trigger": { "record": { "Number": "INC0001001", "Priority": 1, "ShortDescription": "  Mail down  ", "Tags": ["vip", "mail"], "Active": true } },
          "steps": { "lookup": { "count": 2, "records": [ { "Name": "Ada" }, { "Name": "Linus" } ] } },
          "vars": { "approver": "Grace", "evil": "{{trigger.record.Number}}" },
          "_internal": { "secret": "x" }
        }
        """)!;

    [Theory]
    [InlineData("{{trigger.record.Number}}", "INC0001001")]
    [InlineData("{{ trigger.record.Number }}", "INC0001001")]
    [InlineData("{{trigger.record.number}}", "INC0001001")]
    [InlineData("P{{trigger.record.Priority}} – {{vars.approver}}", "P1 – Grace")]
    [InlineData("{{steps.lookup.count}}", "2")]
    [InlineData("{{steps.lookup.records[1].Name}}", "Linus")]
    [InlineData("{{steps.lookup.records.0.Name}}", "Ada")]
    [InlineData("{{steps.lookup.records.first.Name}}", "Ada")]
    [InlineData("{{trigger.record.Tags.count}}", "2")]
    [InlineData("{{trigger.record.Active}}", "true")]
    [InlineData("{{trigger.record.Missing}}", "")]
    [InlineData("{{nothing.here.at.all}}", "")]
    [InlineData("no pills", "no pills")]
    public void Render_ResolvesPaths(string template, string expected)
    {
        Assert.Equal(expected, FlowTemplate.Render(template, s_data));
    }

    [Theory]
    [InlineData("{{trigger.record.ShortDescription | trim}}", "Mail down")]
    [InlineData("{{trigger.record.ShortDescription | trim | upper}}", "MAIL DOWN")]
    [InlineData("{{vars.approver | lower}}", "grace")]
    [InlineData("{{trigger.record.Missing | default:'n/a'}}", "n/a")]
    [InlineData("{{vars.approver | default:'n/a'}}", "Grace")]
    [InlineData("{{trigger.record.Tags | json}}", "[\"vip\",\"mail\"]")]
    [InlineData("q={{trigger.record.ShortDescription | trim | urlencode}}", "q=Mail%20down")]
    [InlineData("{{vars.approver | unknownfilter}}", "Grace")]
    public void Render_AppliesWhitelistedFilters(string template, string expected)
    {
        Assert.Equal(expected, FlowTemplate.Render(template, s_data));
    }

    [Fact]
    public void Render_IsSinglePass_ValuesAreNeverEvaluatedAgain()
    {
        Assert.Equal("{{trigger.record.Number}}", FlowTemplate.Render("{{vars.evil}}", s_data));
    }

    [Theory]
    [InlineData("{{_internal.secret}}")]
    [InlineData("{{trigger.record.Number; System.IO.File.Delete('x')}}")]
    [InlineData("{{trigger.record.Number()}}")]
    [InlineData("{{a.b.c.d.e.f.g.h.i.j.k.l.m.n.o.p.q}}")]
    public void Render_RejectsPrivateOrInvalidPaths(string template)
    {
        Assert.Equal(string.Empty, FlowTemplate.Render(template, s_data));
        Assert.Null(FlowTemplate.Lookup(s_data, template.Trim('{', '}')));
    }

    [Fact]
    public void ResolveValue_KeepsTypeForSingleExpression()
    {
        var number = FlowTemplate.ResolveValue("{{trigger.record.Priority}}", s_data);
        var array = FlowTemplate.ResolveValue(" {{trigger.record.Tags}} ", s_data);
        var mixed = FlowTemplate.ResolveValue("Priority {{trigger.record.Priority}}", s_data);

        Assert.Equal(1, number!.GetValue<int>());
        Assert.IsType<JsonArray>(array);
        Assert.Equal("Priority 1", mixed!.GetValue<string>());
        Assert.Null(FlowTemplate.ResolveValue(null, s_data));
    }

    [Fact]
    public void ResolveValue_ReturnsCopy()
    {
        var copy = (JsonObject)FlowTemplate.ResolveValue("{{trigger.record}}", s_data)!;
        copy["Number"] = "changed";

        Assert.Equal("INC0001001", FlowTemplate.Render("{{trigger.record.Number}}", s_data));
    }

    [Fact]
    public void ExtractPaths_ListsAllExpressions()
    {
        Assert.Equal(["trigger.record.Number", "vars.approver"], FlowTemplate.ExtractPaths("{{trigger.record.Number}} by {{ vars.approver | upper }}"));
        Assert.Empty(FlowTemplate.ExtractPaths("plain"));
    }

    [Fact]
    public void Render_TruncatesHugeOutput()
    {
        var big = new JsonObject { ["v"] = new string('a', 150_000) };

        var rendered = FlowTemplate.Render("{{v}}{{v}}", big);

        Assert.Equal(FlowTemplate.MaxOutputLength, rendered.Length);
    }
}
