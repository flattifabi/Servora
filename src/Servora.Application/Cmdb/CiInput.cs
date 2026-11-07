using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>Editable fields of a configuration item.</summary>
public sealed class CiInput
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the class key.</summary>
    public string ClassName { get; set; } = "server";

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the operational status.</summary>
    public OperationalStatus OperationalStatus { get; set; } = OperationalStatus.Operational;

    /// <summary>Gets or sets the environment.</summary>
    public string? Environment { get; set; }

    /// <summary>Gets or sets the owner.</summary>
    public Guid? OwnedById { get; set; }

    /// <summary>Gets or sets the support group.</summary>
    public Guid? SupportGroupId { get; set; }

    /// <summary>Gets or sets the IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Gets or sets the FQDN.</summary>
    public string? Fqdn { get; set; }

    /// <summary>Gets or sets the serial number.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Gets or sets the manufacturer.</summary>
    public string? Manufacturer { get; set; }

    /// <summary>Gets or sets the model.</summary>
    public string? Model { get; set; }

    /// <summary>Gets or sets the location.</summary>
    public string? Location { get; set; }

    /// <summary>Gets or sets the criticality (1–4).</summary>
    public int Criticality { get; set; } = 3;

    /// <summary>Gets or sets the class attribute values keyed by attribute name (invariant strings).</summary>
    public Dictionary<string, string?> Attributes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the expected concurrency version (0 for new CIs).</summary>
    public long Version { get; set; }
}
