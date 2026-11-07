namespace Servora.Installer.Cli;

/// <summary>Result of parsing the command line.</summary>
/// <param name="Options">The parsed options (defaults when parsing failed).</param>
/// <param name="Errors">Parse errors; empty on success.</param>
internal sealed record CommandLineParseResult(InstallerOptions Options, IReadOnlyList<string> Errors)
{
    /// <summary>Gets a value indicating whether the command line was valid.</summary>
    public bool IsValid => Errors.Count == 0;
}
