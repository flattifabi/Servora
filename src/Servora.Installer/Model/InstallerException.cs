namespace Servora.Installer.Model;

/// <summary>
/// An expected installer failure with an operator-facing message (for example a failed <c>sc.exe</c> call or an
/// invalid directory). Unexpected failures surface as other exception types.
/// </summary>
internal sealed class InstallerException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="InstallerException"/> class.</summary>
    /// <param name="message">The operator-facing message.</param>
    /// <param name="exitCode">The exit code the installer should return.</param>
    public InstallerException(string message, ExitCode exitCode = ExitCode.Failed)
        : base(message)
    {
        ExitCode = exitCode;
    }

    /// <summary>Initializes a new instance of the <see cref="InstallerException"/> class with an inner exception.</summary>
    /// <param name="message">The operator-facing message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public InstallerException(string message, Exception innerException)
        : base(message, innerException)
    {
        ExitCode = ExitCode.Failed;
    }

    /// <summary>Gets the exit code the installer should return for this failure.</summary>
    public ExitCode ExitCode { get; }
}
