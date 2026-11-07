namespace Servora.Application.Automation.Actions;

/// <summary>
/// A step type of the flow engine (extension point). Register implementations as scoped services
/// (<c>services.AddScoped&lt;IFlowAction, MyAction&gt;()</c>); the designer palette and the engine discover them
/// automatically. Actions must be deterministic about their side effects on resume and must never put secrets into
/// their outputs.
/// </summary>
public interface IFlowAction
{
    /// <summary>Gets the designer metadata (type key, fields, outputs, branches).</summary>
    FlowActionDescriptor Descriptor { get; }

    /// <summary>Executes (or, for waiting steps, re-checks) the step.</summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">The cancellation token (run cancellation and engine shutdown).</param>
    /// <returns>The step result.</returns>
    Task<FlowActionResult> ExecuteAsync(FlowActionContext context, CancellationToken cancellationToken);
}
