namespace Servora.Application.Configuration;

/// <summary>Progress state of a <see cref="SetupStep"/>.</summary>
public enum SetupStepStatus
{
    /// <summary>Not started yet.</summary>
    Pending = 0,

    /// <summary>In progress.</summary>
    Running = 1,

    /// <summary>Completed successfully.</summary>
    Done = 2,

    /// <summary>Failed.</summary>
    Failed = 3,
}
