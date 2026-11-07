using Servora.Application.Common;

namespace Servora.Application.Api;

/// <summary>Lifecycle management of API keys (requires <c>admin.apikeys.manage</c>).</summary>
public interface IApiKeyService
{
    /// <summary>Lists keys.</summary>
    /// <param name="page">Paging, sorting (<c>Name</c>, <c>CreatedAt</c>, <c>LastUsedAt</c>, <c>ExpiresAt</c>) and search.</param>
    /// <param name="status">An optional status filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of keys.</returns>
    Task<Result<PagedResult<ApiKeySummary>>> ListAsync(PageRequest page, ApiKeyStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>Gets a key.</summary>
    /// <param name="id">The key identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The details.</returns>
    Task<Result<ApiKeyDetails>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets the KPIs and the 14-day usage series.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The statistics.</returns>
    Task<Result<ApiKeyStats>> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the daily usage of a key.</summary>
    /// <param name="id">The key identifier.</param>
    /// <param name="days">The number of days (1–90).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>One point per day, oldest first.</returns>
    Task<Result<IReadOnlyList<ApiKeyUsagePoint>>> GetUsageAsync(Guid id, int days = 30, CancellationToken cancellationToken = default);

    /// <summary>Gets the scopes the current user may grant (limited to the user's own permissions).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The grantable scopes.</returns>
    Task<Result<IReadOnlyList<GrantableScope>>> GetGrantableScopesAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a key. The plain token is returned once.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The token.</returns>
    Task<Result<ApiKeySecretResult>> CreateAsync(ApiKeyCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates name, description, scopes, expiry, IP ranges and rate limit of an active key.</summary>
    /// <param name="id">The key identifier.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateAsync(Guid id, ApiKeyUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Replaces the secret of a key. The previous token stops working immediately.</summary>
    /// <param name="id">The key identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new token.</returns>
    Task<Result<ApiKeySecretResult>> RotateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Revokes a key permanently.</summary>
    /// <param name="id">The key identifier.</param>
    /// <param name="reason">An optional reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RevokeAsync(Guid id, string? reason, CancellationToken cancellationToken = default);
}
