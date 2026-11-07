namespace Servora.Application.Automation.Actions;

/// <summary>Looks up registered <see cref="IFlowAction"/>s by type key.</summary>
/// <param name="actions">All registered actions.</param>
public sealed class FlowActionRegistry(IEnumerable<IFlowAction> actions)
{
    private readonly Dictionary<string, IFlowAction> _actions = actions
        .GroupBy(a => a.Descriptor.Type, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the descriptors of all actions ordered by category and name.</summary>
    public IReadOnlyList<FlowActionDescriptor> Descriptors =>
        _actions.Values.Select(a => a.Descriptor).OrderBy(d => d.Category).ThenBy(d => d.Name).ToList();

    /// <summary>Finds an action.</summary>
    /// <param name="type">The type key.</param>
    /// <returns>The action or <see langword="null"/>.</returns>
    public IFlowAction? Find(string? type) => type is not null && _actions.TryGetValue(type, out var action) ? action : null;
}
