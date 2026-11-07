namespace Servora.Domain.Sla;

/// <summary>Lifecycle stage of a <see cref="TaskSla"/>. Whether the target was missed is tracked separately in <see cref="TaskSla.HasBreached"/>.</summary>
public enum SlaStage
{
    /// <summary>The timer is running.</summary>
    InProgress = 0,

    /// <summary>The timer is paused (pause condition matches).</summary>
    Paused = 1,

    /// <summary>The stop condition matched; the timer is final.</summary>
    Completed = 2,

    /// <summary>The cancel condition matched or the record was deleted.</summary>
    Cancelled = 3,
}
