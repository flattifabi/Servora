namespace Servora.Domain.Itsm;

/// <summary>How an incident was resolved. Mandatory when an incident is resolved.</summary>
public enum IncidentResolutionCode
{
    /// <summary>The cause was removed permanently.</summary>
    SolvedPermanently = 1,

    /// <summary>Service restored with a workaround; the cause remains.</summary>
    SolvedWorkaround = 2,

    /// <summary>Solved remotely by the service desk.</summary>
    SolvedRemotely = 3,

    /// <summary>The issue could not be reproduced.</summary>
    NotReproducible = 4,

    /// <summary>A fix is not economically justified.</summary>
    NotSolvedTooCostly = 5,

    /// <summary>The caller withdrew or closed the incident.</summary>
    ClosedByCaller = 6,

    /// <summary>The incident duplicates another record.</summary>
    Duplicate = 7,
}
