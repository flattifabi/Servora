using Servora.Application.Automation.Actions;
using Servora.Application.Common;

namespace Servora.Application.Automation.Services;

/// <summary>Flow Designer use cases: list, design, version, activate, test and delete flows.</summary>
public interface IFlowService
{
    /// <summary>Lists flows (<c>automation.read</c>).</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of flows.</returns>
    Task<Result<PagedResult<FlowListItem>>> ListAsync(FlowListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Gets the key figures of the automation area (<c>automation.read</c>).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The figures.</returns>
    Task<Result<FlowOverview>> GetOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a flow definition (<c>automation.read</c>).</summary>
    /// <param name="id">The flow.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definition.</returns>
    Task<Result<FlowDefinitionModel>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates an inactive flow (<c>automation.manage</c>).</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new flow identifier.</returns>
    Task<Result<Guid>> CreateAsync(FlowCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Saves a definition as a new version (<c>automation.manage</c>, optimistic concurrency).</summary>
    /// <param name="model">The definition.</param>
    /// <param name="versionNote">A note describing the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The saved definition.</returns>
    Task<Result<FlowDefinitionModel>> SaveAsync(FlowDefinitionModel model, string? versionNote, CancellationToken cancellationToken = default);

    /// <summary>Validates a definition without saving (no permission needed beyond read).</summary>
    /// <param name="model">The definition.</param>
    /// <param name="forActivation">Whether completeness rules apply.</param>
    /// <returns>The issues.</returns>
    IReadOnlyList<string> Validate(FlowDefinitionModel model, bool forActivation);

    /// <summary>Activates or deactivates a flow (<c>automation.manage</c>).</summary>
    /// <param name="id">The flow.</param>
    /// <param name="active">The new state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default);

    /// <summary>Duplicates a flow as an inactive copy (<c>automation.manage</c>).</summary>
    /// <param name="id">The flow.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The copy's identifier.</returns>
    Task<Result<Guid>> DuplicateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a flow and its webhook secret; run history is kept (<c>automation.manage</c>).</summary>
    /// <param name="id">The flow.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new webhook secret; the plain value is returned once (<c>automation.manage</c>).</summary>
    /// <param name="id">The flow.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new secret.</returns>
    Task<Result<string>> RotateWebhookSecretAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Starts a test run of the saved definition, also for inactive flows (<c>automation.manage</c>).</summary>
    /// <param name="id">The flow.</param>
    /// <param name="request">The test parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The run identifier.</returns>
    Task<Result<Guid>> StartTestRunAsync(Guid id, FlowTestRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets the available step types.</summary>
    /// <returns>The descriptors.</returns>
    IReadOnlyList<FlowActionDescriptor> GetActions();

    /// <summary>Lists all flows for pickers (sub-flows) (<c>automation.read</c>).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The flows.</returns>
    Task<IReadOnlyList<FlowSummary>> ListAllAsync(CancellationToken cancellationToken = default);
}
