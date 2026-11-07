namespace Servora.Infrastructure.Backup;

/// <summary>Provides the configured archive encryption password to background operations.</summary>
public interface IBackupPasswordSource
{
    /// <summary>Reads the configured password.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The password, or <see langword="null"/> when none is configured.</returns>
    Task<string?> GetPasswordAsync(CancellationToken cancellationToken = default);
}
