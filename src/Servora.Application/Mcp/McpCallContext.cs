namespace Servora.Application.Mcp;

/// <summary>Transport-level context of an MCP request (taken from the authenticated API key principal).</summary>
/// <param name="ApiKeyId">The API key identifier.</param>
/// <param name="Scopes">The scopes granted to the key (tool scopes are checked against these).</param>
/// <param name="ActorName">The caller display name.</param>
/// <param name="IpAddress">The client address.</param>
public sealed record McpCallContext(Guid? ApiKeyId, IReadOnlySet<string> Scopes, string ActorName, string? IpAddress);
