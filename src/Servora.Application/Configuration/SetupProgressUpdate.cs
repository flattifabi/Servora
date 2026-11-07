namespace Servora.Application.Configuration;

/// <summary>A progress notification of the installation.</summary>
/// <param name="Step">The step.</param>
/// <param name="Status">The new status.</param>
/// <param name="Message">An optional user-safe message.</param>
public sealed record SetupProgressUpdate(SetupStep Step, SetupStepStatus Status, string? Message = null);
