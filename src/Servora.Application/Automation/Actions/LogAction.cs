using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Servora.Application.Tables;

namespace Servora.Application.Automation.Actions;

/// <summary>Writes a message to the run history and the application log.</summary>
/// <param name="logger">The logger.</param>
public sealed class LogAction(ILogger<LogAction> logger) : IFlowAction
{
    /// <summary>The type key.</summary>
    public const string TypeKey = "flow.log";

    /// <inheritdoc />
    public FlowActionDescriptor Descriptor { get; } = new(TypeKey, "Log message", "Flow logic", "terminal", "neutral", "Write a message to the execution history and application log.")
    {
        Fields =
        [
            new FlowActionField("level", "Level", FlowActionFieldKind.Select) { DefaultValue = "info", SupportsPills = false, Options = [new FieldChoice("info", "Information"), new FieldChoice("warning", "Warning"), new FieldChoice("error", "Error")] },
            new FlowActionField("message", "Message", FlowActionFieldKind.Multiline) { Required = true },
        ],
        Outputs = [new FlowActionOutput("message", "Message")],
    };

    /// <inheritdoc />
    public Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken)
    {
        var message = Engine.FlowRedactor.Truncate(context.GetText("message"), 4000) ?? string.Empty;
        var level = context.GetRaw("level")?.ToLowerInvariant() switch
        {
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            _ => LogLevel.Information,
        };
        logger.Log(level, "Flow {FlowName} (run {RunId}): {Message}", context.FlowName, context.RunId, message);
        return Task.FromResult(FlowActionResult.Success(new JsonObject { ["message"] = message, ["level"] = level.ToString() }));
    }
}
