using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Tables;
using Servora.Domain.Common;
using Servora.Domain.Platform;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Platform;

/// <summary>
/// Table registry of the Platform module: typed tables discovered by reflection merged with administrator-defined
/// custom tables and custom fields. The merged catalog is cached until <see cref="InvalidateAsync"/> is called.
/// </summary>
/// <param name="scopeFactory">The scope factory (metadata is read in a short-lived system scope).</param>
/// <param name="logger">The logger.</param>
public sealed class PlatformTableRegistry(IServiceScopeFactory scopeFactory, ILogger<PlatformTableRegistry> logger) : ReflectionTableRegistry
{
    private const int MaxCustomTables = 2000;
    private const int MaxCustomFields = 20_000;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile IReadOnlyList<TableDefinition>? _cache;
    private long _generation;

    /// <inheritdoc />
    public override async Task<IReadOnlyList<TableDefinition>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is { } cached)
        {
            return cached;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cache is { } again)
            {
                return again;
            }

            var generation = Interlocked.Read(ref _generation);
            var tables = await LoadAsync(cancellationToken);
            if (tables is null)
            {
                return TypedTables;
            }

            if (generation == Interlocked.Read(ref _generation))
            {
                _cache = tables;
            }

            return tables;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public override Task InvalidateAsync()
    {
        Interlocked.Increment(ref _generation);
        _cache = null;
        return Task.CompletedTask;
    }

    private async Task<IReadOnlyList<TableDefinition>?> LoadAsync(CancellationToken cancellationToken)
    {
        List<CustomTable> customTables;
        List<CustomField> customFields;
        try
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("Table Registry"));
            var factory = scope.ServiceProvider.GetRequiredService<IAppDbContextFactory>();
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            customTables = await db.Set<CustomTable>().AsNoTracking().OrderBy(t => t.Name).Take(MaxCustomTables).ToListAsync(cancellationToken);
            customFields = await db.Set<CustomField>().AsNoTracking().OrderBy(f => f.TableName).ThenBy(f => f.Order).Take(MaxCustomFields).ToListAsync(cancellationToken);
        }
        catch (DatabaseNotConfiguredException)
        {
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Custom table metadata could not be loaded; only typed tables are available.");
            return null;
        }

        var fieldsByTable = customFields.ToLookup(f => f.TableName, StringComparer.OrdinalIgnoreCase);
        var result = new List<TableDefinition>(TypedTables.Count + customTables.Count);
        foreach (var typed in TypedTables)
        {
            result.Add(typed.EntityType is { } type && typeof(IHasCustomFields).IsAssignableFrom(type)
                ? CustomTableDefinitionBuilder.WithCustomFields(typed, fieldsByTable[typed.Name])
                : typed);
        }

        foreach (var table in customTables)
        {
            if (result.Any(t => string.Equals(t.Name, table.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(CustomTableDefinitionBuilder.BuildCustomTable(table, fieldsByTable[table.Name]));
        }

        return result.OrderBy(t => t.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
