namespace Servora.Infrastructure.Backup;

/// <summary>
/// Ensures that only one backup or restore runs at a time: an in-process semaphore plus an exclusive lock file in
/// <c>&lt;data dir&gt;/backups</c>, so the command line and the service cannot run concurrently either.
/// </summary>
/// <param name="settings">The settings store (lock file location).</param>
public sealed class BackupOperationLock(BackupSettingsStore settings)
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private volatile string? _current;

    /// <summary>Gets the description of the running operation, or <see langword="null"/> when idle.</summary>
    public string? CurrentOperation => _current;

    /// <summary>Tries to acquire the lock.</summary>
    /// <param name="operation">A description of the operation (shown in the UI).</param>
    /// <param name="wait">How long to wait for a running operation in this process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A handle that releases the lock when disposed, or <see langword="null"/> when another operation is running.</returns>
    public async Task<IDisposable?> TryAcquireAsync(string operation, TimeSpan wait, CancellationToken cancellationToken = default)
    {
        if (!await _semaphore.WaitAsync(wait, cancellationToken))
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settings.LockFilePath)!);
            var file = new FileStream(settings.LockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            _current = operation;
            return new BackupOperationLockHandle(this, file);
        }
        catch (IOException)
        {
            // Another process (service or command line) holds the lock file.
            _semaphore.Release();
            return null;
        }
        catch
        {
            _semaphore.Release();
            throw;
        }
    }

    /// <summary>Acquires the lock or throws.</summary>
    /// <param name="operation">A description of the operation.</param>
    /// <param name="wait">How long to wait.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The handle.</returns>
    /// <exception cref="BackupException">Thrown when another operation is running.</exception>
    public async Task<IDisposable> AcquireAsync(string operation, TimeSpan wait, CancellationToken cancellationToken = default) =>
        await TryAcquireAsync(operation, wait, cancellationToken)
        ?? throw new BackupException($"Another backup or restore operation is running{(_current is null ? string.Empty : $" ({_current})")}. Try again later.");

    /// <summary>Releases the lock (called by <see cref="BackupOperationLockHandle"/>).</summary>
    /// <param name="file">The lock file.</param>
    internal void Release(FileStream file)
    {
        _current = null;
        file.Dispose();
        _semaphore.Release();
    }
}
