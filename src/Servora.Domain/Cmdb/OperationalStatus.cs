namespace Servora.Domain.Cmdb;

/// <summary>Operational status of a configuration item.</summary>
public enum OperationalStatus
{
    /// <summary>In production and working.</summary>
    Operational = 1,

    /// <summary>Not operational (outage).</summary>
    NonOperational = 2,

    /// <summary>Planned / being built.</summary>
    Planned = 3,

    /// <summary>Under maintenance.</summary>
    Maintenance = 4,

    /// <summary>Retired.</summary>
    Retired = 5,
}
