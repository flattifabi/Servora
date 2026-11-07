namespace Servora.Installer.Model;

/// <summary>Result of an external process run.</summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="Output">Combined standard output and standard error (trimmed).</param>
internal sealed record ProcessResult(int ExitCode, string Output)
{
    /// <summary>Gets a value indicating whether the process exited with code 0.</summary>
    public bool Succeeded => ExitCode == 0;
}
