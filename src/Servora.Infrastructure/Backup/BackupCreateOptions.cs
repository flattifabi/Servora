using Servora.Domain.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>Options of <see cref="BackupEngine.CreateAsync"/>.</summary>
/// <param name="Kind">The backup kind.</param>
/// <param name="CreatedBy">Who or what creates the backup.</param>
/// <param name="Destination">The destination folder.</param>
public sealed record BackupCreateOptions(BackupKind Kind, string CreatedBy, string Destination)
{
    /// <summary>Gets a value indicating whether attachment binaries are included.</summary>
    public bool IncludeAttachments { get; init; } = true;

    /// <summary>Gets a value indicating whether the instance <c>appsettings.json</c> is included.</summary>
    public bool IncludeInstanceSettings { get; init; }

    /// <summary>Gets the encryption password, or <see langword="null"/> for an unencrypted archive.</summary>
    public string? Password { get; init; }

    /// <summary>Gets the PBKDF2 iteration count (lower values only for tests).</summary>
    public int Iterations { get; init; } = BackupCipher.DefaultIterations;
}
