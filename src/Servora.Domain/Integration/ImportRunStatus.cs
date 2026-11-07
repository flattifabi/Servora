namespace Servora.Domain.Integration;

/// <summary>State of an import run.</summary>
public enum ImportRunStatus
{
    /// <summary>Rows are being staged or transformed.</summary>
    Running = 0,

    /// <summary>All rows were processed without errors.</summary>
    Completed = 1,

    /// <summary>Processing finished but some rows failed.</summary>
    CompletedWithErrors = 2,

    /// <summary>The run failed before rows could be processed (for example the source was unavailable).</summary>
    Failed = 3,
}
