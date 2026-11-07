using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Servora.Application.Automation.Actions;

/// <summary>Sets a flow variable (<c>{{vars.&lt;name&gt;}}</c>) for later steps.</summary>
public sealed partial class SetVariableAction : IFlowAction
{
    /// <summary>The type key.</summary>
    public const string TypeKey = "flow.set";

    /// <inheritdoc />
    public FlowActionDescriptor Descriptor { get; } = new(TypeKey, "Set variable", "Flow logic", "braces", "neutral", "Store a value in a flow variable for later steps.")
    {
        Fields =
        [
            new FlowActionField("name", "Variable name", FlowActionFieldKind.Text) { Required = true, SupportsPills = false, Placeholder = "approver" },
            new FlowActionField("value", "Value", FlowActionFieldKind.Text),
        ],
        Outputs = [new FlowActionOutput("value", "Value")],
    };

    /// <summary>Checks whether a variable name is valid (letter first, then letters, digits, underscore; max 64).</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValidName(string? name) => name is not null && NameRegex().IsMatch(name);

    /// <inheritdoc />
    public Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken)
    {
        var name = context.GetRaw("name")?.Trim();
        if (!IsValidName(name))
        {
            return Task.FromResult(FlowActionResult.Fail("The variable name must start with a letter and contain only letters, digits and underscores."));
        }

        var value = context.GetValue("value");
        context.Data.SetVariable(name!, value);
        return Task.FromResult(FlowActionResult.Success(new JsonObject { ["name"] = name, ["value"] = context.Data.Variables[name!]?.DeepClone() }));
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();
}
