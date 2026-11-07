using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Configuration;

namespace Servora.Infrastructure.Security;

/// <summary>
/// <see cref="ISecretStore"/> that encrypts values with ASP.NET Core Data Protection. Key material is stored outside
/// the database in <c>&lt;data dir&gt;/keys</c> and protected with DPAPI on Windows (see ADR-007).
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="dataProtection">The data protection provider.</param>
/// <param name="clock">The clock.</param>
public sealed class DataProtectionSecretStore(IAppDbContextFactory dbFactory, IDataProtectionProvider dataProtection, IClock clock) : ISecretStore
{
    private const string Purpose = "Servora.SecretStore.v1";
    private readonly IDataProtector _protector = dataProtection.CreateProtector(Purpose);

    /// <inheritdoc />
    public async Task SetAsync(string name, string plainValue, string? description = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(plainValue);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var secret = await db.Set<StoredSecret>().FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
        if (secret is null)
        {
            secret = new StoredSecret { Name = name };
            db.Set<StoredSecret>().Add(secret);
        }
        else
        {
            secret.RotatedAt = clock.UtcNow;
        }

        secret.ProtectedValue = _protector.Protect(plainValue);
        if (description is not null)
        {
            secret.Description = description;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var value = await db.Set<StoredSecret>().AsNoTracking().Where(s => s.Name == name).Select(s => s.ProtectedValue).FirstOrDefaultAsync(cancellationToken);
        return value is null ? null : _protector.Unprotect(value);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<StoredSecret>().AnyAsync(s => s.Name == name, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var secret = await db.Set<StoredSecret>().FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
        if (secret is not null)
        {
            db.Set<StoredSecret>().Remove(secret);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
