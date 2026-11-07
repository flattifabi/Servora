namespace Servora.Infrastructure.Backup;

/// <summary>Handle returned by <see cref="BackupOperationLock"/>; disposing it releases the lock exactly once.</summary>
/// <param name="owner">The lock.</param>
/// <param name="file">The exclusive lock file.</param>
internal sealed class BackupOperationLockHandle(BackupOperationLock owner, FileStream file) : IDisposable
{
    private int _disposed;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            owner.Release(file);
        }
    }
}
