namespace Servora.Domain.Itsm;

/// <summary>How a problem was resolved. Mandatory when a problem is resolved.</summary>
public enum ProblemResolutionCode
{
    /// <summary>A permanent fix was applied.</summary>
    FixApplied = 1,

    /// <summary>The risk was accepted; no fix will be applied.</summary>
    RiskAccepted = 2,

    /// <summary>The problem duplicates another problem.</summary>
    Duplicate = 3,
}
