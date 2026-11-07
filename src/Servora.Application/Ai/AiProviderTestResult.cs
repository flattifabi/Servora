namespace Servora.Application.Ai;

/// <summary>Outcome of a provider connection test.</summary>
/// <param name="Succeeded">Whether the provider answered.</param>
/// <param name="Message">A user-safe message.</param>
/// <param name="LatencyMs">The round-trip time.</param>
/// <param name="Reply">The (truncated) model reply.</param>
public sealed record AiProviderTestResult(bool Succeeded, string Message, long LatencyMs, string? Reply);
