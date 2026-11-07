using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Servora.Installer.Cli;

namespace Servora.Installer.Platform;

/// <summary>Restarts <c>ServoraSetup.exe</c> through the UAC prompt (<c>runas</c> verb) with the same arguments.</summary>
[SupportedOSPlatform("windows")]
internal static class ElevatedRelauncher
{
    private const int ErrorCancelled = 1223;

    /// <summary>Relaunches the current executable elevated and waits for it.</summary>
    /// <param name="args">The original arguments.</param>
    /// <returns>The exit code of the elevated process, or <see langword="null"/> when the operator declined the UAC prompt.</returns>
    /// <exception cref="InvalidOperationException">The executable path is unknown.</exception>
    public static int? Relaunch(IReadOnlyList<string> args)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("The installer path is unknown.");
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory,
        };
        foreach (var argument in args)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(CommandLineParser.ElevatedSwitch);

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return null;
        }
    }
}
