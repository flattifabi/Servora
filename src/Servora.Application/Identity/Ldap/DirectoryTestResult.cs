namespace Servora.Application.Identity.Ldap;

/// <summary>Result of a directory connection test.</summary>
/// <param name="Succeeded">Whether connecting, securing and binding succeeded.</param>
/// <param name="Message">A user-safe summary.</param>
/// <param name="Steps">The individual steps with their outcome (for example "TLS negotiated", "Service account bound").</param>
/// <param name="ElapsedMilliseconds">The total duration.</param>
public sealed record DirectoryTestResult(bool Succeeded, string Message, IReadOnlyList<string> Steps, long ElapsedMilliseconds);
