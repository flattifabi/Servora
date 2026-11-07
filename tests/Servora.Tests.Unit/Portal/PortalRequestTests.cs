using Servora.Application.Catalog;
using Servora.Application.Common;
using Servora.Application.Portal;
using Servora.Application.Security;
using Servora.Domain.Catalog;
using Servora.Domain.Identity;

namespace Servora.Tests.Unit.Portal;

/// <summary>
/// Portal requests: ordering through the catalog module, requester / requested-for visibility, status timeline, comments,
/// cancellation and approver details – and proof that other users cannot see, comment on or cancel the request.
/// </summary>
public sealed class PortalRequestTests : IDisposable
{
    private readonly PortalTestHost _host = new();

    [Fact]
    public async Task Requester_SeesOwnRequest_WithAnswersAndTimeline_OtherUserSeesNothing()
    {
        var desk = await _host.AddGroupAsync("Desk", GroupType.Assignment);
        var itemId = await _host.AddCatalogItemAsync("New laptop", null, desk);
        var anna = await _host.AddUserAsync("Anna Employee");
        var mallory = await _host.AddUserAsync("Mallory Other");

        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var order = await _host.Get<ICatalogOrderService>().OrderAsync(itemId, new Dictionary<string, string?> { ["reason"] = "Old one broke" });
        Assert.True(order.Succeeded, order.Error);

        var tickets = _host.Get<IPortalTicketService>();
        var list = (await tickets.ListAsync(new PortalTicketQuery(Kind: PortalTicketKind.Request))).Value!;
        var row = Assert.Single(list.Items);
        Assert.Equal(order.Value!.RequestNumber, row.Number);
        Assert.Equal("laptop", row.Icon);

        var detail = (await tickets.GetRequestAsync(order.Value.RequestId)).Value!;
        Assert.Equal("In progress", detail.Status.Label);
        var item = Assert.Single(detail.Items);
        Assert.Contains(item.Answers, a => a.Label == "Business reason" && a.Value == "Old one broke");
        Assert.Equal(1, item.TaskCount);
        Assert.Contains(detail.Steps, s => s.Label == "Fulfilment" && s.State == PortalStepState.Current);
        Assert.True(detail.CanCancel);
        Assert.True((await _host.Get<IPortalConversationService>().AddCommentAsync(PortalTicketKind.Request, detail.Id, "Please deliver to room 4.12")).Succeeded);

        _host.User.Become(mallory, "Mallory Other", Permissions.Portal.Access);
        tickets = _host.Get<IPortalTicketService>();
        Assert.Empty((await tickets.ListAsync(new PortalTicketQuery(PortalTicketFilter.All))).Value!.Items);
        Assert.Equal(ResultErrorKind.NotFound, (await tickets.GetRequestAsync(detail.Id)).ErrorKind);
        Assert.Equal(ResultErrorKind.NotFound, (await tickets.CancelRequestAsync(detail.Id, "not mine")).ErrorKind);
        Assert.Equal(ResultErrorKind.NotFound, (await _host.Get<IPortalConversationService>().GetAsync(PortalTicketKind.Request, detail.Id)).ErrorKind);
        Assert.Equal(ResultErrorKind.NotFound, (await _host.Get<IPortalConversationService>().AddCommentAsync(PortalTicketKind.Request, detail.Id, "hi")).ErrorKind);

        // The owner can cancel; afterwards the request is closed for comments.
        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        tickets = _host.Get<IPortalTicketService>();
        Assert.True((await tickets.CancelRequestAsync(detail.Id, "No longer needed")).Succeeded);
        var cancelled = (await tickets.GetRequestAsync(detail.Id)).Value!;
        Assert.Equal("Cancelled", cancelled.Status.Label);
        Assert.False(cancelled.CanComment);
        Assert.Contains(cancelled.Steps, s => s.State == PortalStepState.Failed);
        Assert.Single((await tickets.ListAsync(new PortalTicketQuery(PortalTicketFilter.Closed))).Value!.Items);
    }

    [Fact]
    public async Task RequestedForUser_SeesRequestOrderedOnTheirBehalf()
    {
        var desk = await _host.AddGroupAsync("Desk", GroupType.Assignment);
        var itemId = await _host.AddCatalogItemAsync("Monitor", null, desk);
        var anna = await _host.AddUserAsync("Anna Employee");
        var fiona = await _host.AddUserAsync("Fiona Fulfiller");

        _host.User.Become(fiona, "Fiona Fulfiller", Permissions.Requests.Read, Permissions.Requests.Write);
        var order = await _host.Get<ICatalogOrderService>().OrderAsync(itemId, new Dictionary<string, string?> { ["reason"] = "Second screen" }, anna);
        Assert.True(order.Succeeded, order.Error);

        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var detail = await _host.Get<IPortalTicketService>().GetRequestAsync(order.Value!.RequestId);
        Assert.True(detail.Succeeded, detail.Error);
        Assert.Equal("Anna Employee", detail.Value!.RequestedForName);
        Assert.Equal("Fiona Fulfiller", detail.Value.OpenedByName);
    }

    [Fact]
    public async Task ApprovalRequired_ShowsWaitingForApproval_AndApproverSeesDetails_OthersDoNot()
    {
        var approver = await _host.AddUserAsync("Ann Approver");
        var board = await _host.AddGroupAsync("Approvers", GroupType.Approval, approver);
        var desk = await _host.AddGroupAsync("Desk", GroupType.Assignment);
        var itemId = await _host.AddCatalogItemAsync("Admin rights", board, desk);
        var anna = await _host.AddUserAsync("Anna Employee");
        var mallory = await _host.AddUserAsync("Mallory Other");

        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var order = (await _host.Get<ICatalogOrderService>().OrderAsync(itemId, new Dictionary<string, string?> { ["reason"] = "Install dev tools" })).Value!;
        var detail = (await _host.Get<IPortalTicketService>().GetRequestAsync(order.RequestId)).Value!;
        Assert.Equal("Waiting for approval", detail.Status.Label);
        Assert.Contains(detail.Steps, s => s.Label == "Approval" && s.State == PortalStepState.Current);
        Assert.Equal("Waiting for approval", detail.Items.Single().ApprovalLabel);

        _host.User.Become(approver, "Ann Approver", Permissions.Portal.Access);
        var pending = (await _host.Get<Application.Approvals.IApprovalInboxService>().ListAsync(Application.Approvals.ApprovalInboxFilter.MyPending, new PageRequest())).Value!;
        var approval = Assert.Single(pending.Items);
        var subject = await _host.Get<IPortalApprovalService>().GetSubjectAsync(approval.Id);
        Assert.True(subject.Succeeded, subject.Error);
        Assert.Contains(subject.Value!.Answers, a => a.Value == "Install dev tools");
        Assert.Contains(subject.Value.Facts, f => f.Label == "Requested for" && f.Value == "Anna Employee");

        // The approver is not the requester – the request itself stays invisible in their "my items".
        Assert.Equal(ResultErrorKind.NotFound, (await _host.Get<IPortalTicketService>().GetRequestAsync(order.RequestId)).ErrorKind);

        _host.User.Become(mallory, "Mallory Other", Permissions.Portal.Access);
        Assert.Equal(ResultErrorKind.NotFound, (await _host.Get<IPortalApprovalService>().GetSubjectAsync(approval.Id)).ErrorKind);
        Assert.False((await _host.Get<Application.Approvals.IApprovalInboxService>().ApproveAsync(approval.Id, null)).Succeeded);

        _host.User.Become(approver, "Ann Approver", Permissions.Portal.Access);
        Assert.True((await _host.Get<Application.Approvals.IApprovalInboxService>().ApproveAsync(approval.Id, "ok")).Succeeded);

        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var approved = (await _host.Get<IPortalTicketService>().GetRequestAsync(order.RequestId)).Value!;
        Assert.Equal("In progress", approved.Status.Label);
        Assert.Equal("Approved", approved.Items.Single().ApprovalLabel);
        Assert.Contains(approved.Steps, s => s.Label == "Approval" && s.State == PortalStepState.Completed);
    }

    [Fact]
    public async Task Home_CountsOnlyOwnItems_AndRanksPopularCatalogItems()
    {
        var desk = await _host.AddGroupAsync("Desk", GroupType.Assignment);
        var laptop = await _host.AddCatalogItemAsync("Laptop", null, desk);
        var mouse = await _host.AddCatalogItemAsync("Mouse", null, desk);
        var anna = await _host.AddUserAsync("Anna Employee");
        var bob = await _host.AddUserAsync("Bob Builder");

        _host.User.Become(bob, "Bob Builder", Permissions.Portal.Access);
        for (var i = 0; i < 3; i++)
        {
            Assert.True((await _host.Get<ICatalogOrderService>().OrderAsync(mouse, new Dictionary<string, string?> { ["reason"] = "spare" })).Succeeded);
        }

        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        Assert.True((await _host.Get<ICatalogOrderService>().OrderAsync(laptop, new Dictionary<string, string?> { ["reason"] = "new" })).Succeeded);
        var home = (await _host.Get<IPortalHomeService>().GetHomeAsync()).Value!;
        Assert.Equal(1, home.OpenRequests);
        Assert.Equal(0, home.OpenIncidents);
        Assert.Single(home.RecentItems);
        Assert.Equal("Mouse", home.PopularItems[0].Name);
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();
}
