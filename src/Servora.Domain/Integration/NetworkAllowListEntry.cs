using Servora.Domain.Common;

namespace Servora.Domain.Integration;

/// <summary>
/// Administrator approval for outbound requests to a network range that the SSRF guard blocks by default
/// (loopback, private, link-local, …). Cloud metadata addresses can never be allowed.
/// </summary>
public class NetworkAllowListEntry : AuditableEntity
{
    /// <summary>Gets or sets the CIDR range (for example <c>10.20.0.0/16</c>) or single IP address.</summary>
    public string Cidr { get; set; } = string.Empty;

    /// <summary>Gets or sets the justification.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets a value indicating whether the entry is in effect.</summary>
    public bool IsEnabled { get; set; } = true;
}
