namespace Servora.Installer.Model;

/// <summary>A payload extracted into a temporary staging directory.</summary>
/// <param name="StagingRoot">The temporary root directory (deleted after the workflow).</param>
/// <param name="ApplicationDirectory">The directory that contains <c>Servora.exe</c>.</param>
/// <param name="Version">The product version of the payload, or <see langword="null"/> when unknown.</param>
internal sealed record StagedPayload(string StagingRoot, string ApplicationDirectory, string? Version);
