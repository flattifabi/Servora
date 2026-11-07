namespace Servora.Application.Observability;

/// <summary>Static and live facts about the Servora process.</summary>
/// <param name="Version">The product version.</param>
/// <param name="Framework">The .NET runtime description.</param>
/// <param name="OperatingSystem">The operating system description.</param>
/// <param name="MachineName">The host name.</param>
/// <param name="ProcessorCount">The number of logical processors.</param>
/// <param name="StartedAt">The UTC process start time.</param>
/// <param name="Uptime">The uptime.</param>
/// <param name="Current">The latest resource sample.</param>
/// <param name="Gen0Collections">Generation 0 garbage collections.</param>
/// <param name="Gen1Collections">Generation 1 garbage collections.</param>
/// <param name="Gen2Collections">Generation 2 garbage collections.</param>
/// <param name="DataDirectory">The data directory.</param>
/// <param name="DatabaseProvider">The configured database provider key, or <see langword="null"/> in setup mode.</param>
/// <param name="Environment">The hosting environment name.</param>
public sealed record ProcessInfo(
    string Version,
    string Framework,
    string OperatingSystem,
    string MachineName,
    int ProcessorCount,
    DateTimeOffset StartedAt,
    TimeSpan Uptime,
    ProcessSample Current,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    string DataDirectory,
    string? DatabaseProvider,
    string Environment);
