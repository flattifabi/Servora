namespace Servora.Application.Backup;

/// <summary>Stops the application so that it restarts (Windows service recovery) and runs pending startup work such as a restore.</summary>
public interface IApplicationRestarter
{
    /// <summary>Gets a value indicating whether the process is restarted automatically (Windows service) or must be restarted manually (console).</summary>
    bool RestartsAutomatically { get; }

    /// <summary>Stops the application after a short delay with a non-zero exit code.</summary>
    /// <param name="reason">The reason written to the log.</param>
    void RequestRestart(string reason);
}
