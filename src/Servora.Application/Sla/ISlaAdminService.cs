using Servora.Application.Common;

namespace Servora.Application.Sla;

/// <summary>Manages SLA definitions and monitors timers (permission <c>platform.sla.manage</c>).</summary>
public interface ISlaAdminService
{
    /// <summary>Lists SLA definitions with their running timer counts.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definitions.</returns>
    Task<Result<IReadOnlyList<SlaDefinitionModel>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets an SLA definition.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definition.</returns>
    Task<Result<SlaDefinitionModel>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates an SLA definition.</summary>
    /// <param name="model">The definition.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> SaveAsync(SlaDefinitionModel model, CancellationToken cancellationToken = default);

    /// <summary>Deletes an SLA definition and its timers.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets monitoring figures and the most urgent running timers.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The overview.</returns>
    Task<Result<SlaOverview>> GetOverviewAsync(CancellationToken cancellationToken = default);
}
