using Servora.Application.Common;

namespace Servora.Application.BusinessRules;

/// <summary>Manages declarative business rules (permission <c>platform.businessrules.manage</c>).</summary>
public interface IBusinessRuleService
{
    /// <summary>Lists business rules.</summary>
    /// <param name="tableName">An optional table filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rules (at most 1000).</returns>
    Task<Result<IReadOnlyList<BusinessRuleModel>>> ListAsync(string? tableName = null, CancellationToken cancellationToken = default);

    /// <summary>Gets a business rule.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rule.</returns>
    Task<Result<BusinessRuleModel>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a business rule.</summary>
    /// <param name="model">The rule (an empty id creates a new rule).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> SaveAsync(BusinessRuleModel model, CancellationToken cancellationToken = default);

    /// <summary>Activates or deactivates a business rule.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="active">The new state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default);

    /// <summary>Deletes a business rule.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
