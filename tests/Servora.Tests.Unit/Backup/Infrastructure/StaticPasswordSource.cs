using Servora.Infrastructure.Backup;

namespace Servora.Tests.Unit.Backup.Infrastructure;

/// <summary><see cref="IBackupPasswordSource"/> returning a settable password.</summary>
internal sealed class StaticPasswordSource : IBackupPasswordSource
{
    /// <summary>Gets or sets the password.</summary>
    public string? Password { get; set; }

    /// <inheritdoc />
    public Task<string?> GetPasswordAsync(CancellationToken cancellationToken = default) => Task.FromResult(Password);
}
