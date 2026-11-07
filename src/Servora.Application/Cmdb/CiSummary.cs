using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>A configuration item row for lists.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Name">The name.</param>
/// <param name="ClassKey">The class key.</param>
/// <param name="ClassLabel">The class label.</param>
/// <param name="ClassIcon">The class icon.</param>
/// <param name="ClassColor">The class color.</param>
/// <param name="Status">The operational status.</param>
/// <param name="Environment">The environment.</param>
/// <param name="IpAddress">The IP address.</param>
/// <param name="Fqdn">The FQDN.</param>
/// <param name="Location">The location.</param>
/// <param name="Criticality">The criticality (1–4).</param>
/// <param name="SupportGroupName">The support group name.</param>
/// <param name="UpdatedAt">The last update.</param>
public sealed record CiSummary(
    Guid Id,
    string Name,
    string ClassKey,
    string ClassLabel,
    string ClassIcon,
    string ClassColor,
    OperationalStatus Status,
    string? Environment,
    string? IpAddress,
    string? Fqdn,
    string? Location,
    int Criticality,
    string? SupportGroupName,
    DateTimeOffset UpdatedAt);
