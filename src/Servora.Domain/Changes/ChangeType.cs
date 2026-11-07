namespace Servora.Domain.Changes;

/// <summary>Type of a change request (ITIL change models).</summary>
public enum ChangeType
{
    /// <summary>Regular change that is assessed and authorized by the change advisory board.</summary>
    Normal = 0,

    /// <summary>Pre-approved, low risk, routine change that skips assessment and authorization.</summary>
    Standard = 1,

    /// <summary>Urgent change that must be implemented as soon as possible (expedited authorization).</summary>
    Emergency = 2,
}
