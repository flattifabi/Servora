using Servora.Application.Common;

namespace Servora.Application.Ai;

/// <summary>
/// Plans a user request, delegates to sub-agents (tool calling where the provider supports it, keyword routing
/// otherwise) and returns the answer with a full execution trace. Agents run with scoped identities; consequential
/// actions are only proposed and executed after explicit user confirmation.
/// </summary>
public interface IAiOrchestrator
{
    /// <summary>Gets the registered agents.</summary>
    IReadOnlyList<IAiAgent> Agents { get; }

    /// <summary>Runs a request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result or a user-safe failure (for example a provider error).</returns>
    Task<Result<AiOrchestrationResult>> RunAsync(AiOrchestrationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Executes a previously proposed consequential action after the user confirmed it.</summary>
    /// <param name="request">The action.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bounded result text or a failure.</returns>
    Task<Result<string>> ExecuteConfirmedActionAsync(AiActionRequest request, CancellationToken cancellationToken = default);
}
