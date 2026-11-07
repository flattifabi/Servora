using Servora.Domain.Common;

namespace Servora.Domain.Cmdb;

/// <summary>
/// A configuration item (CI) in the CMDB: a server, application, service, database, network device, etc.
/// Class-specific attributes are stored as custom fields; <see cref="ClassName"/> selects the CI class.
/// </summary>
[RecordTable(TableNames.ConfigurationItem, "Configuration Item", PluralLabel = "Configuration Items", DisplayField = nameof(Name), Icon = "server", Route = "/cmdb")]
public class ConfigurationItem : AuditableEntity, IHasCustomFields
{
    /// <summary>Gets or sets the CI name (host name, application name, ...).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the CI class key, for example <c>server</c>, <c>application</c> or <c>business_service</c>.</summary>
    public string ClassName { get; set; } = "server";

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the operational status.</summary>
    public OperationalStatus OperationalStatus { get; set; } = OperationalStatus.Operational;

    /// <summary>Gets or sets the environment, for example <c>Production</c>, <c>Test</c> or <c>Development</c>.</summary>
    public string? Environment { get; set; }

    /// <summary>Gets or sets the business owner.</summary>
    [Reference(TableNames.User)]
    public Guid? OwnedById { get; set; }

    /// <summary>Gets or sets the group that supports the CI.</summary>
    [Reference(TableNames.Group)]
    public Guid? SupportGroupId { get; set; }

    /// <summary>Gets or sets the primary IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Gets or sets the fully qualified domain name.</summary>
    public string? Fqdn { get; set; }

    /// <summary>Gets or sets the serial number.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Gets or sets the manufacturer / vendor.</summary>
    public string? Manufacturer { get; set; }

    /// <summary>Gets or sets the model or product version.</summary>
    public string? Model { get; set; }

    /// <summary>Gets or sets the physical or logical location.</summary>
    public string? Location { get; set; }

    /// <summary>Gets or sets the business criticality (1 = most critical .. 4 = least critical).</summary>
    public int Criticality { get; set; } = 3;

    /// <summary>Gets or sets the identifier of the source system for discovered / imported CIs.</summary>
    public string? DiscoverySource { get; set; }

    /// <summary>Gets or sets the UTC time the CI was last discovered or imported.</summary>
    public DateTimeOffset? LastDiscoveredAt { get; set; }

    /// <inheritdoc />
    public string? CustomFieldsJson { get; set; }
}
