namespace Servora.Application.Automation.Engine;

/// <summary>Tuning of the flow engine and scheduler (configuration section <c>Servora:Automation</c>).</summary>
public sealed class FlowEngineOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Servora:Automation";

    /// <summary>Gets or sets the maximum number of runs executed in parallel (1–32).</summary>
    public int MaxConcurrentRuns { get; set; } = 4;

    /// <summary>Gets or sets how often waiting runs are checked, in seconds.</summary>
    public int ResumePollSeconds { get; set; } = 15;

    /// <summary>Gets or sets how often the scheduler looks for due schedules, in seconds.</summary>
    public int SchedulerIntervalSeconds { get; set; } = 20;

    /// <summary>Gets or sets how often a pending approval is re-checked, in seconds.</summary>
    public int ApprovalPollSeconds { get; set; } = 30;

    /// <summary>Gets or sets the fallback re-check interval of a waiting sub-flow step, in seconds.</summary>
    public int SubflowPollSeconds { get; set; } = 60;

    /// <summary>Gets or sets the maximum number of step executions in one run (loop / runaway guard).</summary>
    public int MaxStepExecutionsPerRun { get; set; } = 500;

    /// <summary>Gets or sets the timeout of a single step execution, in seconds.</summary>
    public int StepTimeoutSeconds { get; set; } = 300;

    /// <summary>Gets or sets the maximum sub-flow nesting depth.</summary>
    public int MaxSubflowDepth { get; set; } = 5;

    /// <summary>Gets or sets how long completed runs and execution logs are kept, in days.</summary>
    public int RetentionDays { get; set; } = 90;
}
