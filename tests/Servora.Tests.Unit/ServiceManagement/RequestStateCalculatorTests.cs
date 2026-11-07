using Servora.Application.Catalog;
using Servora.Domain.Catalog;

namespace Servora.Tests.Unit.ServiceManagement;

/// <summary>Tests for <see cref="RequestStateCalculator"/>.</summary>
public sealed class RequestStateCalculatorTests
{
    [Fact]
    public void ItemState_FollowsTasks()
    {
        Assert.Null(RequestStateCalculator.ItemStateFromTasks([]));
        Assert.Null(RequestStateCalculator.ItemStateFromTasks([CatalogTaskState.Completed, CatalogTaskState.WorkInProgress]));
        Assert.Equal(RequestItemState.Completed, RequestStateCalculator.ItemStateFromTasks([CatalogTaskState.Completed, CatalogTaskState.Cancelled]));
        Assert.Equal(RequestItemState.Cancelled, RequestStateCalculator.ItemStateFromTasks([CatalogTaskState.Cancelled]));
    }

    [Fact]
    public void RequestState_FollowsItems()
    {
        Assert.Equal(RequestState.Submitted, RequestStateCalculator.RequestStateFromItems([RequestItemState.PendingApproval]));
        Assert.Equal(RequestState.InProgress, RequestStateCalculator.RequestStateFromItems([RequestItemState.PendingApproval, RequestItemState.InFulfillment]));
        Assert.Equal(RequestState.InProgress, RequestStateCalculator.RequestStateFromItems([RequestItemState.PendingApproval, RequestItemState.Completed]));
        Assert.Equal(RequestState.Completed, RequestStateCalculator.RequestStateFromItems([RequestItemState.Completed, RequestItemState.Rejected]));
        Assert.Equal(RequestState.Rejected, RequestStateCalculator.RequestStateFromItems([RequestItemState.Rejected, RequestItemState.Cancelled]));
        Assert.Equal(RequestState.Cancelled, RequestStateCalculator.RequestStateFromItems([RequestItemState.Cancelled]));
    }
}
