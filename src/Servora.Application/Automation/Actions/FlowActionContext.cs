using System.Text.Json.Nodes;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Model;
using Servora.Application.Automation.Templates;

namespace Servora.Application.Automation.Actions;

/// <summary>Everything a <see cref="IFlowAction"/> needs to execute one step.</summary>
public sealed class FlowActionContext
{
    /// <summary>Gets the run identifier.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Gets the flow identifier.</summary>
    public required Guid FlowId { get; init; }

    /// <summary>Gets the flow name.</summary>
    public required string FlowName { get; init; }

    /// <summary>Gets the sub-flow depth of the run.</summary>
    public int Depth { get; init; }

    /// <summary>Gets a value indicating whether the run is a designer test run.</summary>
    public bool IsTest { get; init; }

    /// <summary>Gets the step definition.</summary>
    public required FlowStepDefinition Step { get; init; }

    /// <summary>Gets the run data (trigger, inputs, variables, step outputs).</summary>
    public required FlowRunContext Data { get; init; }

    /// <summary>Gets the current UTC time.</summary>
    public required DateTimeOffset Now { get; init; }

    /// <summary>Gets a value indicating whether the step is resumed after a wait.</summary>
    public bool IsResume { get; init; }

    /// <summary>Gets the state stored by the step when it started waiting.</summary>
    public JsonNode? WaitState { get; init; }

    /// <summary>Gets the raw (unresolved) configuration value.</summary>
    /// <param name="key">The field key.</param>
    /// <returns>The raw value.</returns>
    public string? GetRaw(string key) => Step.GetConfig(key);

    /// <summary>Gets a configuration value with data pills rendered as text.</summary>
    /// <param name="key">The field key.</param>
    /// <returns>The rendered text (empty when not configured).</returns>
    public string GetText(string key) => FlowTemplate.Render(Step.GetConfig(key), Data.Data);

    /// <summary>Gets a configuration value resolved to a typed value (a single pill keeps its type).</summary>
    /// <param name="key">The field key.</param>
    /// <returns>The value.</returns>
    public JsonNode? GetValue(string key) => FlowTemplate.ResolveValue(Step.GetConfig(key), Data.Data);

    /// <summary>Renders an arbitrary template against the run data.</summary>
    /// <param name="template">The template.</param>
    /// <returns>The rendered text.</returns>
    public string Render(string? template) => FlowTemplate.Render(template, Data.Data);

    /// <summary>Resolves an arbitrary template to a typed value.</summary>
    /// <param name="template">The template.</param>
    /// <returns>The value.</returns>
    public JsonNode? Resolve(string? template) => FlowTemplate.ResolveValue(template, Data.Data);

    /// <summary>Gets an integer configuration value.</summary>
    /// <param name="key">The field key.</param>
    /// <param name="fallback">The fallback.</param>
    /// <returns>The value.</returns>
    public int GetInt(string key, int fallback) =>
        int.TryParse(GetText(key).Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
