using Servora.Application.Common;
using Servora.Application.Events;
using Servora.Domain.Common;

namespace Servora.Application.Tables;

/// <summary>
/// Keeps the custom field index of typed tables up to date when other modules change
/// <see cref="IHasCustomFields.CustomFieldsJson"/> directly (for example through the <c>CustomFieldsSection</c> component).
/// Custom table records are indexed synchronously by the record service.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="registry">The table registry.</param>
public sealed class RecordFieldIndexHandler(IAppDbContextFactory dbFactory, ITableRegistry registry) : IRecordEventHandler
{
    /// <inheritdoc />
    public int Order => 10;

    /// <inheritdoc />
    public async Task HandleAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken)
    {
        var table = await registry.GetTableAsync(recordEvent.TableName, cancellationToken);
        if (table is null || table.IsCustom || !table.Fields.Any(f => f.IsCustom))
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (recordEvent.ChangeType == RecordChangeType.Deleted)
        {
            await RecordFieldIndexer.RemoveAsync(db, table.Name, recordEvent.RecordId, cancellationToken);
            return;
        }

        if (recordEvent.HasChanged(nameof(IHasCustomFields.CustomFieldsJson)))
        {
            await RecordFieldIndexer.SyncAsync(db, table, recordEvent.RecordId, RecordEntityAccessor.FilterExposed(table, recordEvent.Snapshot), cancellationToken);
        }
    }
}
