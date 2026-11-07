using System.Globalization;
using System.Text.Json.Nodes;
using Servora.Application.Tables;

namespace Servora.Application.Automation.Actions;

/// <summary>Pauses the run for a duration or until a point in time. The background resumer continues the run.</summary>
public sealed class WaitAction : IFlowAction
{
    /// <summary>The type key.</summary>
    public const string TypeKey = "flow.wait";

    /// <summary>The longest supported wait.</summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromDays(90);

    /// <inheritdoc />
    public FlowActionDescriptor Descriptor { get; } = new(TypeKey, "Wait", "Flow logic", "timer", "warning", "Pause the flow for a duration or until a date.")
    {
        Fields =
        [
            new FlowActionField("mode", "Wait", FlowActionFieldKind.Select) { DefaultValue = "duration", SupportsPills = false, Options = [new FieldChoice("duration", "For a duration"), new FieldChoice("until", "Until a date / time")] },
            new FlowActionField("minutes", "Duration (minutes)", FlowActionFieldKind.Number) { DefaultValue = "5" },
            new FlowActionField("until", "Until (ISO 8601, UTC)", FlowActionFieldKind.Text) { Placeholder = "{{trigger.record.DueAt}}" },
        ],
        Outputs = [new FlowActionOutput("resumedAt", "Resumed at")],
        CanWait = true,
    };

    /// <inheritdoc />
    public Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken)
    {
        DateTimeOffset until;
        if (context.IsResume && context.WaitState?["until"] is JsonValue stored && DateTimeOffset.TryParse(Templates.FlowTemplate.ToText(stored), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var persisted))
        {
            until = persisted;
        }
        else if (string.Equals(context.GetRaw("mode"), "until", StringComparison.OrdinalIgnoreCase))
        {
            if (!DateTimeOffset.TryParse(context.GetText("until"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out until))
            {
                return Task.FromResult(FlowActionResult.Fail("The wait date is empty or not a valid date."));
            }
        }
        else
        {
            var minutes = Math.Clamp(context.GetInt("minutes", 5), 0, (int)MaxWait.TotalMinutes);
            until = context.Now.AddMinutes(minutes);
        }

        if (until > context.Now + MaxWait)
        {
            until = context.Now + MaxWait;
        }

        if (until <= context.Now)
        {
            return Task.FromResult(FlowActionResult.Success(new JsonObject { ["resumedAt"] = context.Now.ToString("O", CultureInfo.InvariantCulture) }));
        }

        var state = new JsonObject { ["until"] = until.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) };
        return Task.FromResult(FlowActionResult.Wait(until, $"Waiting until {until.ToUniversalTime():yyyy-MM-dd HH:mm} UTC", state));
    }
}
