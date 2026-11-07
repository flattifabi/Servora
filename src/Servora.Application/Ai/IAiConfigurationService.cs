using Servora.Application.Common;

namespace Servora.Application.Ai;

/// <summary>Administration of AI providers and sub-agents (requires <c>admin.ai.manage</c>).</summary>
public interface IAiConfigurationService
{
    /// <summary>Lists the providers.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The providers.</returns>
    Task<Result<IReadOnlyList<AiProviderSummary>>> ListProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a provider. API keys go to the secret store.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provider identifier.</returns>
    Task<Result<Guid>> SaveProviderAsync(AiProviderSaveRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a provider and its stored API key.</summary>
    /// <param name="id">The provider identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteProviderAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Sends a short test prompt to a provider and records the outcome.</summary>
    /// <param name="id">The provider identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The test result.</returns>
    Task<Result<AiProviderTestResult>> TestProviderAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists the sub-agents.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The agents.</returns>
    Task<Result<IReadOnlyList<AiAgentInfo>>> ListAgentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Enables or disables a sub-agent.</summary>
    /// <param name="agentKey">The agent key.</param>
    /// <param name="enabled">The new state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SetAgentEnabledAsync(string agentKey, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>Gets usage KPIs.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The statistics.</returns>
    Task<Result<AiUsageStats>> GetUsageAsync(CancellationToken cancellationToken = default);
}
