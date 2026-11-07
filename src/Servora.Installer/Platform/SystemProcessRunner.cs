using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Installer.Platform;

/// <summary>
/// <see cref="IProcessRunner"/> based on <see cref="Process"/>. Tools are started by full path (no PATH lookup) with an
/// argument list (no shell), so neither a planted executable nor shell metacharacters can change what runs.
/// </summary>
/// <param name="environment">The environment (Windows directory for <c>explorer.exe</c>).</param>
[SupportedOSPlatform("windows")]
internal sealed class SystemProcessRunner(IInstallerEnvironment environment) : IProcessRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => Append(output, e.Data);
        process.ErrorDataReceived += (_, e) => Append(output, e.Data);

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InstallerException($"Could not start {Path.GetFileName(fileName)}: {ex.Message}", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(Timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new InstallerException($"{Path.GetFileName(fileName)} did not finish within {Timeout.TotalSeconds:0} seconds.");
        }

        process.WaitForExit();
        lock (output)
        {
            return new ProcessResult(process.ExitCode, output.ToString().Trim());
        }
    }

    /// <inheritdoc />
    public void OpenUrl(Uri url)
    {
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Only http(s) URLs can be opened.");
        }

        // explorer.exe hands the URL to the default browser of the interactive (non-elevated) user session, so the
        // browser does not inherit the installer's administrator token.
        var startInfo = new ProcessStartInfo(Path.Combine(environment.WindowsDirectory, "explorer.exe"))
        {
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(url.AbsoluteUri);
        using var process = Process.Start(startInfo);
    }

    private static void Append(StringBuilder output, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (output)
        {
            output.AppendLine(line);
        }
    }
}
