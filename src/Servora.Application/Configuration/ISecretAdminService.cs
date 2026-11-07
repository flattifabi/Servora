using Servora.Application.Common;

namespace Servora.Application.Configuration;

/// <summary>
/// Administration of stored secrets (<c>/admin/secrets</c>). Values are write-only: they can be created and rotated but
/// never read back through this service. Every method requires <c>admin.secrets.manage</c> and every change is audited
/// (with the secret name only).
/// </summary>
public interface ISecretAdminService
{
    /// <summary>Lists secret metadata.</summary>
    /// <param name="request">Paging, search (name / description) and sorting (<c>Name</c>, <c>ValueSetAt</c>, <c>UpdatedAt</c>).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<Result<PagedResult<SecretSummary>>> ListAsync(PageRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets the key figures.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The statistics.</returns>
    Task<Result<SecretStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new secret.</summary>
    /// <param name="name">The unique name.</param>
    /// <param name="description">An optional description.</param>
    /// <param name="value">The plain value (never stored unencrypted, never logged).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result; conflict when the name exists.</returns>
    Task<Result> CreateAsync(string name, string? description, string value, CancellationToken cancellationToken = default);

    /// <summary>Replaces the value of an existing secret.</summary>
    /// <param name="name">The name.</param>
    /// <param name="newValue">The new plain value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RotateAsync(string name, string newValue, CancellationToken cancellationToken = default);

    /// <summary>Updates the description of a secret.</summary>
    /// <param name="name">The name.</param>
    /// <param name="description">The new description.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateDescriptionAsync(string name, string? description, CancellationToken cancellationToken = default);

    /// <summary>Deletes a secret permanently.</summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(string name, CancellationToken cancellationToken = default);
}
