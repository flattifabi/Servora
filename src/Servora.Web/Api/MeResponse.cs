namespace Servora.Web.Api;

/// <summary>Response of <c>GET /api/v1/me</c>.</summary>
/// <param name="UserId">The user id (owner of the API key), if any.</param>
/// <param name="Name">The login / principal name.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="ActorType">The actor type (<c>User</c>, <c>ApiKey</c>).</param>
/// <param name="ApiKeyId">The API key id for key principals.</param>
/// <param name="Scopes">The key scopes (empty for interactive users).</param>
/// <param name="CorrelationId">The correlation id of the request.</param>
public sealed record MeResponse(Guid? UserId, string Name, string DisplayName, string ActorType, Guid? ApiKeyId, IReadOnlyList<string> Scopes, string? CorrelationId);
