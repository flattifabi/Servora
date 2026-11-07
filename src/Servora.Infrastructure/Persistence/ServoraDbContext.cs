using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Common;
using Servora.Application.Events;
using Servora.Domain.Common;

namespace Servora.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context of the platform. Entity configurations are discovered from this assembly
/// (<c>Persistence/Configurations/**</c>), so modules never have to edit this class to add entities.
/// </summary>
/// <remarks>
/// <para><c>SaveChangesAsync</c> additionally:</para>
/// <list type="number">
/// <item>maintains <see cref="AuditableEntity"/> audit fields and the optimistic concurrency version,</item>
/// <item>invokes <see cref="IRecordSavingHook"/>s ("before" business rules) for registered tables,</item>
/// <item>publishes <see cref="RecordChangedEvent"/>s after the commit for asynchronous processing.</item>
/// </list>
/// </remarks>
/// <param name="options">The context options.</param>
public sealed class ServoraDbContext(DbContextOptions<ServoraDbContext> options) : DbContext(options), IAppDbContext
{
    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";
    private static readonly ConcurrentDictionary<Type, string?> s_tableNames = new();

    /// <summary>Gets or sets the actor display name written to audit fields.</summary>
    internal string ActorName { get; set; } = "System";

    /// <summary>Gets or sets the actor user identifier used in record events.</summary>
    internal Guid? ActorUserId { get; set; }

    /// <summary>Gets or sets the record event cascade depth for events raised by this context.</summary>
    internal int EventDepth { get; set; }

    /// <summary>Gets or sets the scoped service provider used to resolve hooks and the event publisher.</summary>
    internal IServiceProvider? Services { get; set; }

    /// <summary>Resolves the technical table name for an entity type, or <see langword="null"/> when it is not a table.</summary>
    /// <param name="type">The CLR type.</param>
    /// <returns>The table name.</returns>
    internal static string? GetRecordTableName(Type type) =>
        s_tableNames.GetOrAdd(type, t => t.GetCustomAttribute<RecordTableAttribute>(inherit: false)?.Name);

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync so that hooks and record events are processed.");

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        var now = DateTimeOffset.UtcNow;
        ApplyAuditFields(now);

        var pending = CollectRecordChanges();
        if (pending.Count > 0 && Services is not null)
        {
            await RunSavingHooksAsync(pending, cancellationToken);
            ChangeTracker.DetectChanges();
            ApplyAuditFields(now);
            pending = CollectRecordChanges();
        }

        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        if (pending.Count > 0)
        {
            await PublishEventsAsync(pending, now, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServoraDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Property(nameof(AuditableEntity.Version)).IsConcurrencyToken();
                modelBuilder.Entity(entityType.ClrType).Property(nameof(AuditableEntity.CreatedBy)).HasMaxLength(256);
                modelBuilder.Entity(entityType.ClrType).Property(nameof(AuditableEntity.UpdatedBy)).HasMaxLength(256);
            }
        }
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite cannot compare or order DateTimeOffset values in SQL. All timestamps are UTC, so storing them as
        // binary ticks keeps ordering and comparisons correct (see docs/architecture/database-providers.md).
        if (Database.ProviderName == SqliteProviderName)
        {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
            configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }

    private void ApplyAuditFields(DateTimeOffset now)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = entry.Entity.CreatedAt == default ? now : entry.Entity.CreatedAt;
                    entry.Entity.CreatedBy ??= ActorName;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = ActorName;
                    entry.Entity.Version = Math.Max(1, entry.Entity.Version);
                    break;
                case EntityState.Modified when !entry.Property(nameof(AuditableEntity.Version)).IsModified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = ActorName;
                    entry.Entity.Version++;
                    break;
            }
        }
    }

    private List<PendingRecordChange> CollectRecordChanges()
    {
        var result = new List<PendingRecordChange>();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var tableName = entry.Entity is IDynamicTableRecord dynamic ? dynamic.TableName : GetRecordTableName(entry.Metadata.ClrType);
            if (tableName is null || entry.Entity is not Entity entity)
            {
                continue;
            }

            var changeType = entry.State switch
            {
                EntityState.Added => RecordChangeType.Created,
                EntityState.Deleted => RecordChangeType.Deleted,
                _ => RecordChangeType.Updated,
            };

            var changes = new List<RecordFieldChange>();
            if (changeType != RecordChangeType.Deleted)
            {
                foreach (var property in entry.Properties)
                {
                    var name = property.Metadata.Name;
                    if (name is nameof(AuditableEntity.Version) or nameof(AuditableEntity.UpdatedAt) or nameof(AuditableEntity.UpdatedBy))
                    {
                        continue;
                    }

                    if (changeType == RecordChangeType.Created)
                    {
                        if (property.CurrentValue is not null)
                        {
                            changes.Add(new RecordFieldChange(name, null, property.CurrentValue));
                        }
                    }
                    else if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
                    {
                        changes.Add(new RecordFieldChange(name, property.OriginalValue, property.CurrentValue));
                    }
                }

                if (changeType == RecordChangeType.Updated && changes.Count == 0)
                {
                    continue;
                }
            }

            result.Add(new PendingRecordChange(tableName, entity, entry, changeType, changes));
        }

        return result;
    }

    private async Task RunSavingHooksAsync(List<PendingRecordChange> pending, CancellationToken cancellationToken)
    {
        var hooks = Services!.GetServices<IRecordSavingHook>().OrderBy(h => h.Order).ToList();
        if (hooks.Count == 0)
        {
            return;
        }

        foreach (var change in pending)
        {
            var context = new RecordSavingContext(change.TableName, change.Entity, change.ChangeType, change.Changes);
            foreach (var hook in hooks)
            {
                await hook.OnSavingAsync(context, cancellationToken);
                if (context.IsAborted)
                {
                    throw new RecordSaveAbortedException(context.AbortMessage!);
                }
            }
        }
    }

    private async Task PublishEventsAsync(List<PendingRecordChange> pending, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var publisher = Services?.GetService<IRecordEventPublisher>();
        if (publisher is null)
        {
            return;
        }

        foreach (var change in pending)
        {
            var recordEvent = new RecordChangedEvent(
                change.TableName,
                change.Entity.Id,
                change.ChangeType,
                change.Changes,
                change.Snapshot,
                ActorUserId,
                ActorName,
                now,
                EventDepth);
            await publisher.PublishAsync(recordEvent, cancellationToken);
        }
    }
}
