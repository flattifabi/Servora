namespace Servora.Domain.Changes;

/// <summary>Outcome recorded when a change request is closed.</summary>
public enum ChangeCloseCode
{
    /// <summary>Implemented successfully.</summary>
    Successful = 0,

    /// <summary>Implemented, but with issues.</summary>
    SuccessfulWithIssues = 1,

    /// <summary>Implementation failed or was backed out.</summary>
    Unsuccessful = 2,
}
