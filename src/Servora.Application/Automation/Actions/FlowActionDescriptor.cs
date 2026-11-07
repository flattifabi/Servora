namespace Servora.Application.Automation.Actions;

/// <summary>Designer metadata of a step type.</summary>
/// <param name="Type">The unique type key, for example <c>record.update</c>.</param>
/// <param name="Name">The display name.</param>
/// <param name="Category">The palette category.</param>
/// <param name="Icon">The icon name.</param>
/// <param name="Tone">The accent tone (primary, accent, success, warning, danger, info, purple).</param>
/// <param name="Description">The description shown in the palette.</param>
public sealed record FlowActionDescriptor(string Type, string Name, string Category, string Icon, string Tone, string Description)
{
    /// <summary>Gets the configuration fields.</summary>
    public IReadOnlyList<FlowActionField> Fields { get; init; } = [];

    /// <summary>Gets the outputs.</summary>
    public IReadOnlyList<FlowActionOutput> Outputs { get; init; } = [];

    /// <summary>Gets the branches (empty for linear steps).</summary>
    public IReadOnlyList<FlowActionBranch> Branches { get; init; } = [];

    /// <summary>Gets a value indicating whether the step can pause the run (wait, approval, sub-flow).</summary>
    public bool CanWait { get; init; }
}
