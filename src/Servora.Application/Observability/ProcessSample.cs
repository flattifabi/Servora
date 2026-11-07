namespace Servora.Application.Observability;

/// <summary>One sample of process resource usage.</summary>
/// <param name="At">The UTC sample time.</param>
/// <param name="CpuPercent">CPU usage of the process across all cores (0..100).</param>
/// <param name="WorkingSetBytes">The working set.</param>
/// <param name="ManagedBytes">The managed heap size.</param>
/// <param name="ThreadCount">The number of threads.</param>
public sealed record ProcessSample(DateTimeOffset At, double CpuPercent, long WorkingSetBytes, long ManagedBytes, int ThreadCount);
