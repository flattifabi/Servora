using Servora.Application.Common;
using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>Administration of CI classes and their attribute schemas.</summary>
public interface ICiClassService
{
    /// <summary>Lists all classes (<c>cmdb.read</c>).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The classes ordered by sort order and label.</returns>
    Task<Result<IReadOnlyList<CiClassDto>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the effective attributes of a class including inherited ones, parents first (<c>cmdb.read</c>).</summary>
    /// <param name="classKey">The class key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The attributes.</returns>
    Task<Result<IReadOnlyList<CiAttributeDefinition>>> GetEffectiveAttributesAsync(string classKey, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a class (<c>cmdb.manage</c>).</summary>
    /// <param name="id">The id to update, or <see langword="null"/> to create.</param>
    /// <param name="input">The values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> SaveAsync(Guid? id, CiClassInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes an unused, non-system class without sub-classes (<c>cmdb.manage</c>).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
