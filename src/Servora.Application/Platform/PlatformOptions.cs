namespace Servora.Application.Platform;

/// <summary>Options of the Platform engine (configuration section <c>Servora:Platform</c>).</summary>
public sealed class PlatformOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Servora:Platform";

    /// <summary>Gets or sets the public base URL used for links in e-mails, for example <c>https://servora.example.com</c>.</summary>
    public string? PublicUrl { get; set; }

    /// <summary>Gets or sets the SLA evaluation interval in seconds (minimum 5).</summary>
    public int SlaEvaluationIntervalSeconds { get; set; } = 30;

    /// <summary>Gets or sets the maximum number of SLA timers processed per evaluation batch.</summary>
    public int SlaBatchSize { get; set; } = 200;
}
