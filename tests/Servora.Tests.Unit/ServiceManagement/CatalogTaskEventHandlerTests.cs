using Microsoft.EntityFrameworkCore;
using Servora.Application.Catalog;
using Servora.Application.Events;
using Servora.Application.Security;
using Servora.Domain.Catalog;
using Servora.Domain.Common;
using Servora.Domain.Identity;

namespace Servora.Tests.Unit.ServiceManagement;

/// <summary>
/// Tests of <see cref="CatalogTaskEventHandler"/>: catalog tasks closed by other writers (flows, Table API) still close
/// their requested item and request.
/// </summary>
public sealed class CatalogTaskEventHandlerTests : IDisposable
{
    private readonly ServiceManagementTestHost _host = new();

    [Fact]
    public async Task TasksClosedOutsideTheRequestService_CloseItemAndRequest()
    {
        var desk = await _host.AddGroupAsync("Desk", GroupType.Assignment);
        var itemId = await CreateItemAsync(desk);
        var employee = await _host.AddUserAsync("Eric Employee");
        _host.User.Become(employee, "Eric Employee", Permissions.Portal.Access);
        var order = (await _host.Get<ICatalogOrderService>().OrderAsync(itemId, new Dictionary<string, string?>())).Value!;
        Assert.Equal(RequestItemState.InFulfillment, order.State);

        // Simulate a flow / Table API writer that completes the tasks directly.
        List<CatalogTask> tasks;
        await using (var db = _host.Database.CreateDbContext())
        {
            tasks = await db.Set<CatalogTask>().Where(t => t.RequestItemId == order.RequestItemId).ToListAsync();
            Assert.Equal(2, tasks.Count);
            foreach (var task in tasks)
            {
                task.State = CatalogTaskState.Completed;
            }

            await db.SaveChangesAsync();
        }

        var handler = Handler();
        await handler.HandleAsync(StateChanged(tasks[0]), CancellationToken.None);

        await using (var db = _host.Database.CreateDbContext())
        {
            var item = await db.Set<RequestItem>().AsNoTracking().SingleAsync(i => i.Id == order.RequestItemId);
            var request = await db.Set<Request>().AsNoTracking().SingleAsync(r => r.Id == order.RequestId);
            Assert.Equal(RequestItemState.Completed, item.State);
            Assert.Equal(RequestState.Completed, request.State);
            Assert.False(request.Active);
        }

        // Idempotent: a second event changes nothing.
        await handler.HandleAsync(StateChanged(tasks[1]), CancellationToken.None);
    }

    [Fact]
    public async Task UnrelatedEvents_AreIgnored()
    {
        var desk = await _host.AddGroupAsync("Desk", GroupType.Assignment);
        var itemId = await CreateItemAsync(desk);
        var employee = await _host.AddUserAsync("Eric Employee");
        _host.User.Become(employee, "Eric Employee", Permissions.Portal.Access);
        var order = (await _host.Get<ICatalogOrderService>().OrderAsync(itemId, new Dictionary<string, string?>())).Value!;

        var handler = Handler();
        var snapshot = new Dictionary<string, object?> { [nameof(CatalogTask.RequestItemId)] = order.RequestItemId };
        await handler.HandleAsync(new RecordChangedEvent(TableNames.CatalogTask, Guid.NewGuid(), RecordChangeType.Updated,
            [new RecordFieldChange(nameof(CatalogTask.ShortDescription), "a", "b")], snapshot, null, "system", _host.Clock.UtcNow, 0), CancellationToken.None);
        await handler.HandleAsync(new RecordChangedEvent(TableNames.Incident, Guid.NewGuid(), RecordChangeType.Updated,
            [new RecordFieldChange("State", 1, 2)], snapshot, null, "system", _host.Clock.UtcNow, 0), CancellationToken.None);

        await using var db = _host.Database.CreateDbContext();
        Assert.Equal(RequestItemState.InFulfillment, (await db.Set<RequestItem>().AsNoTracking().SingleAsync(i => i.Id == order.RequestItemId)).State);
    }

    private CatalogTaskEventHandler Handler() =>
        _host.Get<IEnumerable<IRecordEventHandler>>().OfType<CatalogTaskEventHandler>().Single();

    private RecordChangedEvent StateChanged(CatalogTask task) => new(
        TableNames.CatalogTask,
        task.Id,
        RecordChangeType.Updated,
        [new RecordFieldChange(nameof(CatalogTask.State), CatalogTaskState.Open, CatalogTaskState.Completed)],
        new Dictionary<string, object?> { [nameof(CatalogTask.RequestItemId)] = task.RequestItemId, [nameof(CatalogTask.State)] = task.State },
        null,
        "Flow",
        _host.Clock.UtcNow,
        1);

    private async Task<Guid> CreateItemAsync(Guid fulfillmentGroup)
    {
        _host.User.Become(Guid.NewGuid(), "Catalog Admin", Permissions.Requests.ManageCatalog);
        var admin = _host.Get<ICatalogAdminService>();
        var category = await admin.SaveCategoryAsync(new CatalogCategoryModel { Name = "Hardware", Icon = "laptop" });
        Assert.True(category.Succeeded, category.Error);
        var item = await admin.CreateItemAsync(new CatalogItemModel
        {
            Name = "Docking station",
            Icon = "laptop",
            CategoryId = category.Value,
            FulfillmentGroupId = fulfillmentGroup,
            FulfillmentTasks = "Hand out docking station\nConfirm delivery",
        });
        Assert.True(item.Succeeded, item.Error);
        return item.Value;
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();
}
