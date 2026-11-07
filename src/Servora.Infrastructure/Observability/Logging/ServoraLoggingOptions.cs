namespace Servora.Infrastructure.Observability.Logging;

/// <summary>Start-up settings of the Servora logger provider (configuration section <c>Servora:Logging</c>).</summary>
public sealed class ServoraLoggingOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Servora:Logging";

    /// <summary>Gets or sets the number of entries kept in memory for the log viewer.</summary>
    public int RingBufferCapacity { get; set; } = 5000;

    /// <summary>Gets or sets the initial log file retention in days (overridden by the persisted setting).</summary>
    public int RetentionDays { get; set; } = 14;

    /// <summary>Gets or sets the initial maximum log file size in MB (overridden by the persisted setting).</summary>
    public int MaxFileSizeMb { get; set; } = 20;

    /// <summary>Gets or sets the capacity of the file writer queue.</summary>
    public int FileQueueCapacity { get; set; } = 20_000;
}
