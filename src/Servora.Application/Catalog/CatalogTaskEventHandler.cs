using Servora.Application.Events;
using Servora.Domain.Catalog;
using Servora.Domain.Common;

namespace Servora.Application.Catalog;

/// <summary>
/// Keeps requested items and requests in sync when catalog tasks are closed by writers other than
/// <see cref="IRequestService"/> (flows, the generic record API, imports): whenever the state of a catalog task changes,
/// the task states are rolled up to the requested item and the request. The roll-up is idempotent, so changes made
/// through the request service (which already rolls up synchronously) are not processed twice.
/// </summary>
/// <param name="fulfillment">The fulfilment engine.</param>
public sealed class CatalogTaskEventHandler(CatalogFulfillmentEngine fulfillment) : IRecordEventHandler
{
    /// <inheritdoc />
    public int Order => 50;

    /// <inheritdoc />
    public async Task HandleAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recordEvent);
        if (!string.Equals(recordEvent.TableName, TableNames.CatalogTask, StringComparison.OrdinalIgnoreCase)
            || recordEvent.ChangeType == RecordChangeType.Created
            || (recordEvent.ChangeType == RecordChangeType.Updated && !recordEvent.HasChanged(nameof(CatalogTask.State))))
        {
            return;
        }

        if (recordEvent.Snapshot.TryGetValue(nameof(CatalogTask.RequestItemId), out var value) && TryGetGuid(value, out var requestItemId))
        {
            await fulfillment.RecomputeItemAsync(requestItemId, cancellationToken);
        }
    }

    private static bool TryGetGuid(object? value, out Guid id)
    {
        switch (value)
        {
            case Guid guid when guid != Guid.Empty:
                id = guid;
                return true;
            case string text when Guid.TryParse(text, out var parsed):
                id = parsed;
                return true;
            default:
                id = Guid.Empty;
                return false;
        }
    }
}
