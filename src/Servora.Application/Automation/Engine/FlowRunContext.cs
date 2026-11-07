using System.Text.Json;
using System.Text.Json.Nodes;

namespace Servora.Application.Automation.Engine;

/// <summary>
/// The data of one flow run: <c>trigger</c>, <c>inputs</c>, <c>vars</c>, <c>steps</c> (outputs by step key),
/// <c>run</c> and <c>flow</c> – addressable by data pills – plus private engine state (completed steps, chosen
/// branches, wait state) that templates can never read. Persisted as JSON in <c>FlowRun.ContextJson</c> so waiting runs
/// can be resumed by replaying completed steps.
/// </summary>
public sealed class FlowRunContext
{
    /// <summary>The maximum persisted context size in characters.</summary>
    public const int MaxJsonLength = 250_000;

    private const string StateKey = "_state";
    private readonly JsonObject _root;

    private FlowRunContext(JsonObject root)
    {
        _root = root;
        EnsureObject("trigger");
        EnsureObject("inputs");
        EnsureObject("vars");
        EnsureObject("steps");
        EnsureObject("run");
        EnsureObject("flow");
        var state = EnsureObject(StateKey);
        if (state["completed"] is not JsonObject)
        {
            state["completed"] = new JsonObject();
        }

        if (state["wait"] is not JsonObject)
        {
            state["wait"] = new JsonObject();
        }
    }

    /// <summary>Gets the data tree used to resolve templates.</summary>
    public JsonObject Data => _root;

    /// <summary>Gets the trigger data.</summary>
    public JsonObject Trigger => (JsonObject)_root["trigger"]!;

    /// <summary>Gets the inputs.</summary>
    public JsonObject Inputs => (JsonObject)_root["inputs"]!;

    /// <summary>Gets the variables.</summary>
    public JsonObject Variables => (JsonObject)_root["vars"]!;

    /// <summary>Gets the step outputs keyed by step key.</summary>
    public JsonObject Steps => (JsonObject)_root["steps"]!;

    private JsonObject Completed => (JsonObject)((JsonObject)_root[StateKey]!)["completed"]!;

    private JsonObject WaitStates => (JsonObject)((JsonObject)_root[StateKey]!)["wait"]!;

    /// <summary>Creates a new context.</summary>
    /// <param name="trigger">The trigger data (redacted by the caller).</param>
    /// <param name="inputs">The input values.</param>
    /// <param name="runInfo">Run metadata (id, started by, test flag).</param>
    /// <param name="flowInfo">Flow metadata (id, name, version).</param>
    /// <returns>The context.</returns>
    public static FlowRunContext Create(JsonObject? trigger, JsonObject? inputs, JsonObject? runInfo, JsonObject? flowInfo)
    {
        var root = new JsonObject
        {
            ["trigger"] = trigger ?? new JsonObject(),
            ["inputs"] = inputs ?? new JsonObject(),
            ["vars"] = new JsonObject(),
            ["steps"] = new JsonObject(),
            ["run"] = runInfo ?? new JsonObject(),
            ["flow"] = flowInfo ?? new JsonObject(),
        };
        return new FlowRunContext(root);
    }

    /// <summary>Parses a persisted context; invalid JSON yields an empty context.</summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The context.</returns>
    public static FlowRunContext Parse(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                if (JsonNode.Parse(json) is JsonObject obj)
                {
                    return new FlowRunContext(obj);
                }
            }
            catch (JsonException)
            {
                // Fall through to an empty context.
            }
        }

        return Create(null, null, null, null);
    }

    /// <summary>Stores the (redacted) output of a step under <c>steps.&lt;key&gt;</c>.</summary>
    /// <param name="stepKey">The step key.</param>
    /// <param name="output">The output.</param>
    public void SetStepOutput(string stepKey, JsonNode? output) => Steps[stepKey] = FlowRedactor.Redact(output) ?? new JsonObject();

    /// <summary>Sets a variable (<c>vars.&lt;name&gt;</c>); the value is redacted like step outputs.</summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value.</param>
    public void SetVariable(string name, JsonNode? value)
    {
        var redacted = FlowRedactor.IsSensitiveName(name) && value is not null ? JsonValue.Create(FlowRedactor.Mask) : FlowRedactor.Redact(value);
        Variables[name] = redacted;
    }

    /// <summary>Checks whether a step completed in an earlier execution segment.</summary>
    /// <param name="stepId">The step id.</param>
    /// <returns><see langword="true"/> when completed.</returns>
    public bool IsCompleted(string stepId) => Completed.ContainsKey(stepId);

    /// <summary>Gets the branch chosen by a completed step.</summary>
    /// <param name="stepId">The step id.</param>
    /// <returns>The branch key or <see langword="null"/>.</returns>
    public string? GetBranch(string stepId) =>
        Completed.TryGetPropertyValue(stepId, out var node) && node is JsonValue value && value.TryGetValue<string>(out var branch) && branch.Length > 0 ? branch : null;

    /// <summary>Marks a step as completed.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="branch">The chosen branch, if any.</param>
    public void MarkCompleted(string stepId, string? branch)
    {
        Completed[stepId] = branch ?? string.Empty;
        WaitStates.Remove(stepId);
    }

    /// <summary>Gets the wait state of a waiting step.</summary>
    /// <param name="stepId">The step id.</param>
    /// <returns>The state or <see langword="null"/>.</returns>
    public JsonNode? GetWaitState(string stepId) => WaitStates.TryGetPropertyValue(stepId, out var node) ? node : null;

    /// <summary>Checks whether a step is currently waiting.</summary>
    /// <param name="stepId">The step id.</param>
    /// <returns><see langword="true"/> when a wait state exists.</returns>
    public bool IsWaiting(string stepId) => WaitStates.ContainsKey(stepId);

    /// <summary>Stores the wait state of a step.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="state">The state.</param>
    public void SetWaitState(string stepId, JsonNode? state) => WaitStates[stepId] = state?.DeepClone() ?? new JsonObject();

    /// <summary>Returns the public part of the data (without engine state) as redacted JSON for display.</summary>
    /// <returns>The JSON text.</returns>
    public string ToPublicJson()
    {
        var copy = new JsonObject();
        foreach (var pair in _root)
        {
            if (!pair.Key.StartsWith('_'))
            {
                copy[pair.Key] = pair.Value?.DeepClone();
            }
        }

        return FlowRedactor.Redact(copy)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Serializes the context for persistence; oversized step outputs are truncated to stay within the limit.</summary>
    /// <returns>The JSON text.</returns>
    public string Serialize()
    {
        var json = _root.ToJsonString();
        if (json.Length <= MaxJsonLength)
        {
            return json;
        }

        // Shrink the largest step outputs until the context fits.
        foreach (var pair in Steps.OrderByDescending(p => p.Value?.ToJsonString().Length ?? 0).ToList())
        {
            Steps[pair.Key] = new JsonObject { ["_truncated"] = true };
            json = _root.ToJsonString();
            if (json.Length <= MaxJsonLength)
            {
                return json;
            }
        }

        Trigger.Clear();
        Trigger["_truncated"] = true;
        return _root.ToJsonString();
    }

    private JsonObject EnsureObject(string key)
    {
        if (_root[key] is JsonObject obj)
        {
            return obj;
        }

        var created = new JsonObject();
        _root[key] = created;
        return created;
    }
}
