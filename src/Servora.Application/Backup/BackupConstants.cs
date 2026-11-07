namespace Servora.Application.Backup;

/// <summary>Shared constants of the backup module.</summary>
public static class BackupConstants
{
    /// <summary>The text an administrator must type to confirm a restore.</summary>
    public const string RestoreConfirmationText = "RESTORE";

    /// <summary>The file extension of backup archives.</summary>
    public const string FileExtension = ".svbak";

    /// <summary>The <see cref="Security.ISecretStore"/> name of the archive encryption password.</summary>
    public const string PasswordSecretName = "backup.encryption_password";

    /// <summary>The minimum length of the archive encryption password.</summary>
    public const int MinPasswordLength = 12;

    /// <summary>The process exit code used to ask the service manager for a restart (restore pending).</summary>
    public const int RestartExitCode = 75;
}
