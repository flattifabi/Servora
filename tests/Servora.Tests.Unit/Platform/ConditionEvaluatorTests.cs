using Servora.Application.Conditions;
using Servora.Application.Events;
using Servora.Domain.Cmdb;
using Servora.Domain.Itsm;

namespace Servora.Tests.Unit.Platform;

/// <summary>In-memory tests for <see cref="ConditionEvaluator"/>.</summary>
public sealed class ConditionEvaluatorTests
{
    private static readonly Guid s_userId = Guid.Parse("7d0b3a4c-2a50-4b64-9a30-3b5c3f6f3e11");
    private readonly ConditionEvaluator _evaluator = new();

    private static Dictionary<string, object?> Record() => new()
    {
        ["ShortDescription"] = "Email server down",
        ["Priority"] = Priority.High,
        ["Impact"] = Impact.High,
        ["Count"] = 42,
        ["Amount"] = 12.5m,
        ["Ratio"] = 0.75d,
        ["Active"] = true,
        ["AssignedToId"] = s_userId,
        ["OpenedAt"] = new DateTimeOffset(2026, 3, 15, 10, 30, 0, TimeSpan.Zero),
        ["DueDate"] = new DateOnly(2026, 4, 1),
        ["Status"] = OperationalStatus.Operational,
        ["Empty"] = "  ",
        ["Missing"] = null,
        ["u_custom_text"] = "Lisbon",
        ["u_custom_number"] = 7L,
        ["u_custom_date"] = "2026-03-15T10:30:00.0000000+00:00",
        ["u_custom_ref"] = s_userId.ToString(),
    };

    private static ConditionGroup Rule(string field, ConditionOperator op, string? value = null) =>
        new() { Rules = [new ConditionRule { Field = field, Operator = op, Value = value }] };

    [Fact]
    public void NullOrEmptyCondition_AlwaysMatches()
    {
        Assert.True(_evaluator.Evaluate(null, Record()));
        Assert.True(_evaluator.Evaluate(new ConditionGroup(), Record()));
        Assert.True(_evaluator.Evaluate(new ConditionGroup { Rules = [new ConditionRule { Field = " " }] }, Record()));
    }

    [Theory]
    [InlineData("ShortDescription", ConditionOperator.Equals, "email server down", true)]
    [InlineData("ShortDescription", ConditionOperator.NotEquals, "Email server down", false)]
    [InlineData("ShortDescription", ConditionOperator.Contains, "SERVER", true)]
    [InlineData("ShortDescription", ConditionOperator.NotContains, "printer", true)]
    [InlineData("ShortDescription", ConditionOperator.StartsWith, "email", true)]
    [InlineData("ShortDescription", ConditionOperator.EndsWith, "DOWN", true)]
    [InlineData("ShortDescription", ConditionOperator.EndsWith, "up", false)]
    [InlineData("ShortDescription", ConditionOperator.In, "foo, email server down", true)]
    [InlineData("ShortDescription", ConditionOperator.NotIn, "foo,bar", true)]
    public void TextOperators(string field, ConditionOperator op, string value, bool expected) =>
        Assert.Equal(expected, _evaluator.Evaluate(Rule(field, op, value), Record()));

    [Theory]
    [InlineData(ConditionOperator.Equals, "2", true)]
    [InlineData(ConditionOperator.Equals, "High", true)]
    [InlineData(ConditionOperator.Equals, "high", true)]
    [InlineData(ConditionOperator.Equals, "Critical", false)]
    [InlineData(ConditionOperator.LessThan, "3", true)]
    [InlineData(ConditionOperator.GreaterThan, "Critical", true)]
    [InlineData(ConditionOperator.In, "1,2", true)]
    [InlineData(ConditionOperator.In, "Critical, High", true)]
    [InlineData(ConditionOperator.NotIn, "4,5", true)]
    [InlineData(ConditionOperator.Equals, "not-a-priority", false)]
    public void Enums_CoercedByValueOrName(ConditionOperator op, string value, bool expected) =>
        Assert.Equal(expected, _evaluator.Evaluate(Rule("Priority", op, value), Record()));

    [Theory]
    [InlineData("Count", ConditionOperator.Equals, "42", true)]
    [InlineData("Count", ConditionOperator.Equals, "42.0", true)]
    [InlineData("Count", ConditionOperator.GreaterThan, "41", true)]
    [InlineData("Count", ConditionOperator.GreaterOrEqual, "42", true)]
    [InlineData("Count", ConditionOperator.LessThan, "42", false)]
    [InlineData("Count", ConditionOperator.LessOrEqual, "42", true)]
    [InlineData("Count", ConditionOperator.GreaterThan, "abc", false)]
    [InlineData("Amount", ConditionOperator.GreaterThan, "12.4", true)]
    [InlineData("Amount", ConditionOperator.Equals, "12.50", true)]
    [InlineData("Ratio", ConditionOperator.LessThan, "1", true)]
    [InlineData("u_custom_number", ConditionOperator.GreaterThan, "6", true)]
    public void Numbers(string field, ConditionOperator op, string value, bool expected) =>
        Assert.Equal(expected, _evaluator.Evaluate(Rule(field, op, value), Record()));

    [Theory]
    [InlineData("Active", ConditionOperator.Equals, "true", true)]
    [InlineData("Active", ConditionOperator.Equals, "1", true)]
    [InlineData("Active", ConditionOperator.Equals, "yes", true)]
    [InlineData("Active", ConditionOperator.Equals, "false", false)]
    [InlineData("Active", ConditionOperator.NotEquals, "no", true)]
    [InlineData("Active", ConditionOperator.GreaterThan, "false", false)]
    public void Booleans(string field, ConditionOperator op, string value, bool expected) =>
        Assert.Equal(expected, _evaluator.Evaluate(Rule(field, op, value), Record()));

    [Theory]
    [InlineData("OpenedAt", ConditionOperator.Equals, "2026-03-15", true)]
    [InlineData("OpenedAt", ConditionOperator.Equals, "2026-03-15T10:30:00Z", true)]
    [InlineData("OpenedAt", ConditionOperator.Equals, "2026-03-15T11:30:00+01:00", true)]
    [InlineData("OpenedAt", ConditionOperator.GreaterThan, "2026-03-15", true)]
    [InlineData("OpenedAt", ConditionOperator.LessThan, "2026-03-15T09:00:00Z", false)]
    [InlineData("OpenedAt", ConditionOperator.NotEquals, "2026-03-16", true)]
    [InlineData("DueDate", ConditionOperator.Equals, "2026-04-01", true)]
    [InlineData("DueDate", ConditionOperator.LessThan, "2026-05-01", true)]
    [InlineData("u_custom_date", ConditionOperator.Equals, "2026-03-15", true)]
    [InlineData("u_custom_date", ConditionOperator.GreaterThan, "2026-01-01", true)]
    public void Dates(string field, ConditionOperator op, string value, bool expected) =>
        Assert.Equal(expected, _evaluator.Evaluate(Rule(field, op, value), Record()));

    [Fact]
    public void Guids_CoercedFromText()
    {
        Assert.True(_evaluator.Evaluate(Rule("AssignedToId", ConditionOperator.Equals, s_userId.ToString("N")), Record()));
        Assert.True(_evaluator.Evaluate(Rule("AssignedToId", ConditionOperator.Equals, s_userId.ToString().ToUpperInvariant()), Record()));
        Assert.False(_evaluator.Evaluate(Rule("AssignedToId", ConditionOperator.Equals, Guid.NewGuid().ToString()), Record()));
        Assert.True(_evaluator.Evaluate(Rule("u_custom_ref", ConditionOperator.Equals, s_userId.ToString("B")), Record()));
    }

    [Theory]
    [InlineData("Missing", ConditionOperator.IsEmpty, true)]
    [InlineData("Empty", ConditionOperator.IsEmpty, true)]
    [InlineData("Unknown", ConditionOperator.IsEmpty, true)]
    [InlineData("ShortDescription", ConditionOperator.IsEmpty, false)]
    [InlineData("ShortDescription", ConditionOperator.IsNotEmpty, true)]
    [InlineData("Missing", ConditionOperator.IsNotEmpty, false)]
    public void Emptiness(string field, ConditionOperator op, bool expected) =>
        Assert.Equal(expected, _evaluator.Evaluate(Rule(field, op), Record()));

    [Fact]
    public void NullValues_HaveDefinedSemantics()
    {
        Assert.False(_evaluator.Evaluate(Rule("Missing", ConditionOperator.Equals, "x"), Record()));
        Assert.True(_evaluator.Evaluate(Rule("Missing", ConditionOperator.NotEquals, "x"), Record()));
        Assert.True(_evaluator.Evaluate(Rule("Missing", ConditionOperator.Equals, ""), Record()));
        Assert.False(_evaluator.Evaluate(Rule("Missing", ConditionOperator.GreaterThan, "1"), Record()));
        Assert.False(_evaluator.Evaluate(Rule("Missing", ConditionOperator.Contains, "a"), Record()));
        Assert.True(_evaluator.Evaluate(Rule("Missing", ConditionOperator.NotContains, "a"), Record()));
        Assert.True(_evaluator.Evaluate(Rule("Missing", ConditionOperator.NotIn, "a,b"), Record()));
    }

    [Fact]
    public void FieldNames_AreCaseInsensitive()
    {
        var record = new Dictionary<string, object?> { ["shortdescription"] = "abc" };
        Assert.True(_evaluator.Evaluate(Rule("ShortDescription", ConditionOperator.Equals, "ABC"), record));
    }

    [Fact]
    public void IsMe_ComparesWithCurrentUser()
    {
        Assert.True(_evaluator.Evaluate(Rule("AssignedToId", ConditionOperator.IsMe), Record(), new ConditionContext(s_userId)));
        Assert.True(_evaluator.Evaluate(Rule("u_custom_ref", ConditionOperator.IsMe), Record(), new ConditionContext(s_userId)));
        Assert.False(_evaluator.Evaluate(Rule("AssignedToId", ConditionOperator.IsMe), Record(), new ConditionContext(Guid.NewGuid())));
        Assert.False(_evaluator.Evaluate(Rule("AssignedToId", ConditionOperator.IsMe), Record()));
    }

    [Fact]
    public void ChangeOperators_UseEventChanges()
    {
        var changes = new ConditionContext(null, [new RecordFieldChange("Priority", Priority.Low, Priority.High), new RecordFieldChange("u_custom_text", "Porto", "Lisbon")]);

        Assert.True(_evaluator.Evaluate(Rule("Priority", ConditionOperator.Changed), Record(), changes));
        Assert.True(_evaluator.Evaluate(Rule("Priority", ConditionOperator.ChangedTo, "High"), Record(), changes));
        Assert.True(_evaluator.Evaluate(Rule("Priority", ConditionOperator.ChangedTo, "2"), Record(), changes));
        Assert.False(_evaluator.Evaluate(Rule("Priority", ConditionOperator.ChangedTo, "Low"), Record(), changes));
        Assert.True(_evaluator.Evaluate(Rule("Priority", ConditionOperator.ChangedFrom, "Low"), Record(), changes));
        Assert.True(_evaluator.Evaluate(Rule("u_custom_text", ConditionOperator.ChangedFrom, "porto"), Record(), changes));
        Assert.False(_evaluator.Evaluate(Rule("Impact", ConditionOperator.Changed), Record(), changes));
        Assert.False(_evaluator.Evaluate(Rule("Priority", ConditionOperator.Changed), Record()));
    }

    [Fact]
    public void Groups_CombineWithAndOr()
    {
        var condition = new ConditionGroup
        {
            Logic = ConditionLogic.And,
            Rules = [new ConditionRule { Field = "Active", Operator = ConditionOperator.Equals, Value = "true" }],
            Groups =
            [
                new ConditionGroup
                {
                    Logic = ConditionLogic.Or,
                    Rules =
                    [
                        new ConditionRule { Field = "Priority", Operator = ConditionOperator.Equals, Value = "Critical" },
                        new ConditionRule { Field = "ShortDescription", Operator = ConditionOperator.Contains, Value = "server" },
                    ],
                },
            ],
        };
        Assert.True(_evaluator.Evaluate(condition, Record()));

        condition.Groups[0].Rules[1].Value = "printer";
        Assert.False(_evaluator.Evaluate(condition, Record()));

        condition.Logic = ConditionLogic.Or;
        Assert.True(_evaluator.Evaluate(condition, Record()));
    }

    [Fact]
    public void DeepNesting_IsBounded()
    {
        var root = new ConditionGroup();
        var current = root;
        for (var i = 0; i < 20; i++)
        {
            var next = new ConditionGroup();
            current.Groups.Add(next);
            current = next;
        }

        current.Rules.Add(new ConditionRule { Field = "Active", Operator = ConditionOperator.Equals, Value = "true" });
        Assert.False(_evaluator.Evaluate(root, Record()));
    }

    [Fact]
    public void JsonElements_AreNormalized()
    {
        using var document = System.Text.Json.JsonDocument.Parse("""{"n": 5, "s": "abc", "b": true}""");
        var record = new Dictionary<string, object?>
        {
            ["n"] = document.RootElement.GetProperty("n"),
            ["s"] = document.RootElement.GetProperty("s"),
            ["b"] = document.RootElement.GetProperty("b"),
        };
        Assert.True(_evaluator.Evaluate(Rule("n", ConditionOperator.GreaterThan, "4"), record));
        Assert.True(_evaluator.Evaluate(Rule("s", ConditionOperator.Equals, "ABC"), record));
        Assert.True(_evaluator.Evaluate(Rule("b", ConditionOperator.Equals, "true"), record));
    }

    [Fact]
    public void Serializer_RoundTripsConditions()
    {
        var condition = Rule("Priority", ConditionOperator.In, "1,2");
        var json = ConditionSerializer.Serialize(condition);
        Assert.Contains("\"In\"", json);
        var copy = ConditionSerializer.Deserialize(json);
        Assert.Equal(ConditionOperator.In, copy.Rules[0].Operator);
        Assert.True(ConditionSerializer.Deserialize("{ invalid").IsEmpty);
    }
}
