using Servora.Application.Approvals;
using Servora.Application.Catalog;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Approvals;
using Servora.Domain.Catalog;
using Servora.Domain.Identity;

namespace Servora.Tests.Unit.ServiceManagement;

/// <summary>End-to-end tests of catalog building, ordering, approval, fulfilment and closure (in-memory SQLite).</summary>
public sealed class CatalogOrderFlowTests : IDisposable
{
    private readonly ServiceManagementTestHost _host = new();

    [Fact]
    public async Task Order_WithoutApproval_CreatesTasks_AndClosesWhenTasksComplete()
    {
        var fulfiller = await _host.AddUserAsync("Fiona Fulfiller");
        var desk = await _host.AddGroupAsync("Desk", GroupType.Assignment, fulfiller);
        var itemId = await CreateItemAsync(desk, approvalGroup: null, tasks: "Install software\nVerify installation");

        var employee = await _host.AddUserAsync("Eric Employee");
        _host.User.Become(employee, "Eric Employee", Permissions.Portal.Access);
        var order = await _host.Get<ICatalogOrderService>().OrderAsync(itemId, new Dictionary<string, string?> { ["software"] = "7-Zip", ["urgent"] = "yes" });
        Assert.True(order.Succeeded, order.Error);
        Assert.Equal(RequestItemState.InFulfillment, order.Value!.State);

        // The requester sees the request; fulfillers work the tasks.
        var mine = await _host.Get<IRequestService>().ListMyRequestsAsync(new PageRequest());
        Assert.Single(mine.Value!.Items);

        _host.User.Become(fulfiller, "Fiona Fulfiller", Permissions.Requests.Read, Permissions.Requests.Write);
        var requests = _host.Get<IRequestService>();
        var detail = (await requests.GetAsync(order.Value.RequestId)).Value!;
        Assert.Equal(RequestState.InProgress, detail.State);
        var item = Assert.Single(detail.Items);
        Assert.Equal(2, item.Tasks.Count);
        Assert.All(item.Tasks, t => Assert.Equal(desk, t.AssignmentGroupId));
        Assert.Contains(item.Answers, a => a.Label == "Urgent" && a.Value == "Yes");

        var myGroupTasks = await requests.ListTasksAsync(RequestListFilter.MyGroups, new PageRequest());
        Assert.Equal(2, myGroupTasks.Value!.TotalCount);

        foreach (var task in item.Tasks)
        {
            var update = await requests.UpdateTaskAsync(task.Id, new CatalogTaskUpdate(CatalogTaskState.Completed, desk, fulfiller, "Done"));
            Assert.True(update.Succeeded, update.Error);
        }

        detail = (await requests.GetAsync(order.Value.RequestId)).Value!;
        Assert.Equal(RequestState.Completed, detail.State);
        Assert.Equal(RequestItemState.Completed, detail.Items[0].State);
        Assert.False(detail.CanCancel);
    }

    [Fact]
    public async Task Order_WithApproval_WaitsForApprover_ThenFulfils()
    {
        var approver = await _host.AddUserAsync("Ann Approver");
        var board = await _host.AddGroupAsync("Approvers", GroupType.Approval, approver);
        var desk = await _host.AddGroupAsync("Desk", GroupType.Assignment);
        var itemId = await CreateItemAsync(desk, board, tasks: null);

        var employee = await _host.AddUserAsync("Eric Employee");
        _host.User.Become(employee, "Eric Employee", Permissions.Portal.Access);
        var order = await _host.Get<ICatalogOrderService>().OrderAsync(itemId, new Dictionary<string, string?> { ["software"] = "Visio" });
        Assert.True(order.Succeeded, order.Error);
        Assert.True(order.Value!.ApprovalRequired);
        Assert.Equal(RequestItemState.PendingApproval, order.Value.State);

        _host.User.Become(approver, "Ann Approver", Permissions.Portal.Access);
        var inbox = _host.Get<IApprovalInboxService>();
        var pending = Assert.Single((await inbox.ListAsync(ApprovalInboxFilter.MyPending, new PageRequest())).Value!.Items);
        Assert.Equal(order.Value.RequestItemNumber, pending.RecordNumber);
        Assert.Equal($"/requests/{order.Value.RequestId}", pending.RecordRoute);
        Assert.True((await inbox.ApproveAsync(pending.Id, null)).Succeeded);

        _host.User.Become(employee, "Eric Employee", Permissions.Portal.Access);
        var detail = (await _host.Get<IRequestService>().GetAsync(order.Value.RequestId)).Value!;
        Assert.Equal(RequestState.InProgress, detail.State);
        Assert.Equal(ApprovalStatus.Approved, detail.Items[0].Approval);
        Assert.Single(detail.Items[0].Tasks);
        Assert.True(detail.CanCancel);
        Assert.False(detail.CanFulfil);
    }

    [Fact]
    public async Task Rejection_ClosesTheRequest()
    {
        var approver = await _host.AddUserAsync("Ann Approver");
        var board = await _host.AddGroupAsync("Approvers", GroupType.Approval, approver);
        var itemId = await CreateItemAsync(null, board, tasks: null);
        var employee = await _host.AddUserAsync("Eric Employee");
        _host.User.Become(employee, "Eric Employee", Permissions.Portal.Access);
        var order = (await _host.Get<ICatalogOrderService>().OrderAsync(itemId, new Dictionary<string, string?> { ["software"] = "Game" })).Value!;

        _host.User.Become(approver, "Ann Approver", Permissions.Portal.Access);
        var inbox = _host.Get<IApprovalInboxService>();
        var pending = Assert.Single((await inbox.ListAsync(ApprovalInboxFilter.MyPending, new PageRequest())).Value!.Items);
        Assert.True((await inbox.RejectAsync(pending.Id, "Not business related")).Succeeded);

        _host.User.Become(employee, "Eric Employee", Permissions.Portal.Access);
        var detail = (await _host.Get<IRequestService>().GetAsync(order.RequestId)).Value!;
        Assert.Equal(RequestState.Rejected, detail.State);
        Assert.Equal(RequestItemState.Rejected, detail.Items[0].State);
        Assert.Empty(detail.Items[0].Tasks);
    }

    [Fact]
    public async Task Order_ValidatesAnswersAndPermissions()
    {
        var itemId = await CreateItemAsync(null, null, null);
        var employee = await _host.AddUserAsync("Eric Employee");
        var colleague = await _host.AddUserAsync("Carl Colleague");
        var orders = _host.Get<ICatalogOrderService>();

        _host.User.Become(employee, "Eric Employee", Permissions.Portal.Access);
        var missing = await orders.OrderAsync(itemId, new Dictionary<string, string?>());
        Assert.Equal(ResultErrorKind.Validation, missing.ErrorKind);
        Assert.Contains("Software", missing.Error);

        var forOther = await orders.OrderAsync(itemId, new Dictionary<string, string?> { ["software"] = "X" }, colleague);
        Assert.Equal(ResultErrorKind.Forbidden, forOther.ErrorKind);

        _host.User.Become(employee, "Eric Employee");
        Assert.Equal(ResultErrorKind.Forbidden, (await orders.OrderAsync(itemId, new Dictionary<string, string?> { ["software"] = "X" })).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await _host.Get<ICatalogAdminService>().ListCategoriesAsync()).ErrorKind);
    }

    [Fact]
    public async Task Order_RequiringApproval_WithoutApprovers_IsRefusedBeforeCreatingRecords()
    {
        var emptyBoard = await _host.AddGroupAsync("Nobody", GroupType.Approval);
        var itemId = await CreateItemAsync(null, emptyBoard, null);
        var employee = await _host.AddUserAsync("Eric Employee");
        _host.User.Become(employee, "Eric Employee", Permissions.Portal.Access, Permissions.Requests.Read);

        var result = await _host.Get<ICatalogOrderService>().OrderAsync(itemId, new Dictionary<string, string?> { ["software"] = "X" });
        Assert.Equal(ResultErrorKind.Failure, result.ErrorKind);
        Assert.Equal(0, (await _host.Get<IRequestService>().ListRequestsAsync(RequestListFilter.All, new PageRequest())).Value!.TotalCount);
    }

    [Fact]
    public async Task Builder_ManagesVariables()
    {
        var admin = _host.Get<ICatalogAdminService>();
        var itemId = await CreateItemAsync(null, null, null);
        var item = (await admin.GetItemAsync(itemId)).Value!;
        Assert.Equal(["software", "urgent"], item.Variables.Select(v => v.Name).ToArray());

        Assert.Equal(ResultErrorKind.Validation, (await admin.SaveVariableAsync(itemId, new CatalogVariableModel { Name = "software", Label = "Duplicate" })).ErrorKind);
        Assert.True((await admin.MoveVariableAsync(item.Variables[1].Id, -1)).Succeeded);
        item = (await admin.GetItemAsync(itemId)).Value!;
        Assert.Equal("urgent", item.Variables[0].Name);

        var copy = await admin.DuplicateItemAsync(itemId);
        Assert.True(copy.Succeeded);
        Assert.Equal(2, (await admin.GetItemAsync(copy.Value)).Value!.Variables.Count);
        Assert.True((await admin.DeleteItemAsync(copy.Value)).Succeeded);
        Assert.NotEmpty(_host.Audit.Entries);
    }

    private async Task<Guid> CreateItemAsync(Guid? fulfillmentGroup, Guid? approvalGroup, string? tasks)
    {
        _host.User.Become(Guid.NewGuid(), "Catalog Admin", Permissions.Requests.ManageCatalog);
        var admin = _host.Get<ICatalogAdminService>();
        var category = await admin.SaveCategoryAsync(new CatalogCategoryModel { Name = "Software " + Guid.NewGuid().ToString("N")[..6], Icon = "package" });
        Assert.True(category.Succeeded, category.Error);
        var item = await admin.CreateItemAsync(new CatalogItemModel
        {
            Name = "Software installation",
            Icon = "package",
            CategoryId = category.Value,
            FulfillmentGroupId = fulfillmentGroup,
            FulfillmentTasks = tasks,
            ApprovalRequired = approvalGroup is not null,
            ApprovalGroupId = approvalGroup,
        });
        Assert.True(item.Succeeded, item.Error);
        Assert.True((await admin.SaveVariableAsync(item.Value, new CatalogVariableModel { Name = "software", Label = "Software", Mandatory = true })).Succeeded);
        Assert.True((await admin.SaveVariableAsync(item.Value, new CatalogVariableModel { Name = "urgent", Label = "Urgent", Type = CatalogVariableType.Boolean })).Succeeded);
        return item.Value;
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();
}
