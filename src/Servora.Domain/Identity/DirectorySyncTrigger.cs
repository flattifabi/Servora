namespace Servora.Domain.Identity;

/// <summary>What started a <see cref="DirectorySyncRun"/>.</summary>
public enum DirectorySyncTrigger
{
    /// <summary>Started by the background schedule.</summary>
    Scheduled = 0,

    /// <summary>Started by an administrator ("Sync now").</summary>
    Manual = 1,
}
