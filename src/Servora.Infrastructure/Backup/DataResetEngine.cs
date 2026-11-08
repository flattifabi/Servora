using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Servora.Application.Backup;
using Servora.Domain.Analytics;
using Servora.Domain.Identity;
using Servora.Domain.Platform;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Backup;

/// <summary>Counts and removes the records listed in <see cref="DataResetCatalog"/> in one transaction.</summary>
/// <param name="factory">The context factory.</param>
public sealed class DataResetEngine(AppDbContextFactory factory)
{
    private const long FirstRecordNumber = 1001;
    private const string BuiltInAdministrator = "ADMIN";

    /// <summary>Counts the rows a reset would remove.</summary>
    /// <param name="keepUserId">A user account that stays in addition to the administrator and service accounts.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows per area.</returns>
    public async Task<DataResetPreview> CountAsync(Guid? keepUserId, CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        var kept = await KeptUserIdsAsync(db, keepUserId, cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);

        var areas = new List<DataResetArea>();
        foreach (var (area, tables) in DataResetCatalog.Cleared)
        {
            long rows = 0;
            foreach (var table in tables)
            {
                if (TableName(db, table) is { } name)
                {
                    await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
                    command.CommandText = "SELECT COUNT(*) FROM " + name;
                    rows += Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            if (area == DataResetCatalog.UsersArea)
            {
                rows += await db.Set<User>().IgnoreQueryFilters().CountAsync(u => !kept.Contains(u.Id), cancellationToken);
            }

            areas.Add(new DataResetArea(area, rows));
        }

        return new DataResetPreview(areas);
    }

    /// <summary>Removes the records and rewinds the number sequences. Either everything is removed or nothing.</summary>
    /// <param name="keepUserId">A user account that stays in addition to the administrator and service accounts.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of removed rows.</returns>
    public async Task<long> ResetAsync(Guid? keepUserId, CancellationToken cancellationToken)
    {
        await using var db = factory.Create();
        var kept = await KeptUserIdsAsync(db, keepUserId, cancellationToken);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            long deleted = 0;
            foreach (var table in DataResetCatalog.Cleared.SelectMany(a => a.Tables))
            {
                if (TableName(db, table) is { } name)
                {
                    var sql = "DELETE FROM " + name;
                    deleted += await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                }
            }

            await db.Set<DashboardPin>().IgnoreQueryFilters().Where(p => !kept.Contains(p.UserId)).ExecuteDeleteAsync(cancellationToken);
            await db.Set<UserRole>().IgnoreQueryFilters().Where(r => !kept.Contains(r.UserId)).ExecuteDeleteAsync(cancellationToken);
            deleted += await db.Set<User>().IgnoreQueryFilters().Where(u => !kept.Contains(u.Id)).ExecuteDeleteAsync(cancellationToken);
            await db.Set<NumberSequence>().IgnoreQueryFilters().ExecuteUpdateAsync(s => s.SetProperty(n => n.NextValue, FirstRecordNumber), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return deleted;
        });
    }

    private static Task<List<Guid>> KeptUserIdsAsync(ServoraDbContext db, Guid? keepUserId, CancellationToken cancellationToken) =>
        db.Set<User>().IgnoreQueryFilters()
            .Where(u => u.NormalizedUserName == BuiltInAdministrator || u.Source == UserSource.Service || u.Id == keepUserId)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

    /// <summary>Returns the delimited name of a table from the catalog (never user input), or null if the model lacks it.</summary>
    private static string? TableName(ServoraDbContext db, string table)
    {
        var entity = db.Model.GetEntityTypes().FirstOrDefault(e => e.GetTableName() == table);
        return entity is null ? null : db.GetService<ISqlGenerationHelper>().DelimitIdentifier(table, entity.GetSchema());
    }
}
