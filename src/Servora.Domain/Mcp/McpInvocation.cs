using Servora.Domain.Common;

namespace Servora.Domain.Mcp;

/// <summary>Log entry of one MCP request (tool call, resource read or client initialization) for the admin page.</summary>
public class McpInvocation : Entity
{
    /// <summary>Gets or sets the UTC time of the request.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Gets or sets the JSON-RPC method, for example <c>tools/call</c>.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Gets or sets the tool name or resource URI.</summary>
    public string? Target { get; set; }

    /// <summary>Gets or sets the API key identifier of the caller.</summary>
    public Guid? ApiKeyId { get; set; }

    /// <summary>Gets or sets the caller display name.</summary>
    public string? ActorName { get; set; }

    /// <summary>Gets or sets the MCP client name reported during initialization.</summary>
    public string? ClientName { get; set; }

    /// <summary>Gets or sets the outcome.</summary>
    public McpInvocationOutcome Outcome { get; set; }

    /// <summary>Gets or sets the duration in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>Gets or sets a user-safe error message.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the client IP address.</summary>
    public string? IpAddress { get; set; }
}
