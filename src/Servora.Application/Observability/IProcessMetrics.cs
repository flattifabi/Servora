namespace Servora.Application.Observability;

/// <summary>Samples CPU, memory and threads of the Servora process (singleton).</summary>
public interface IProcessMetrics
{
    /// <summary>Gets the UTC start time of the process.</summary>
    DateTimeOffset StartedAt { get; }

    /// <summary>Gets the most recent sample (takes one when none exists yet).</summary>
    ProcessSample Current { get; }

    /// <summary>Gets the recent samples, oldest first (bounded).</summary>
    IReadOnlyList<ProcessSample> History { get; }

    /// <summary>Takes a new sample and appends it to the history.</summary>
    /// <returns>The sample.</returns>
    ProcessSample Sample();
}
