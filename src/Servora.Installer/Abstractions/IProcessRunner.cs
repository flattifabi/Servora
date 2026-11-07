using Servora.Installer.Model;

namespace Servora.Installer.Abstractions;

/// <summary>Runs external system tools (<c>sc.exe</c>, <c>netsh.exe</c>, <c>icacls.exe</c>). Faked in unit tests.</summary>
internal interface IProcessRunner
{
    /// <summary>
    /// Runs a process without a shell and waits for it to exit. Arguments are passed as a list (no string
    /// concatenation, no shell interpretation).
    /// </summary>
    /// <param name="fileName">The full path of the executable.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The exit code and captured output.</returns>
    ProcessResult Run(string fileName, IReadOnlyList<string> arguments);

    /// <summary>Opens a URL in the interactive user's default browser.</summary>
    /// <param name="url">The absolute http(s) URL.</param>
    void OpenUrl(Uri url);
}
