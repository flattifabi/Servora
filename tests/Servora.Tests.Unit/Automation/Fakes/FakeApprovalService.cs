using Servora.Application.Approvals;
using Servora.Application.Common;

namespace Servora.Tests.Unit.Automation.Fakes;

/// <summary><see cref="IApprovalService"/> whose decision is set by the test.</summary>
internal sealed class FakeApprovalService : IApprovalService
{
    /// <summary>Gets the requests made.</summary>
    public List<ApprovalRequest> Requests { get; } = [];

    /// <summary>Gets or sets the state returned by <see cref="GetSummaryAsync"/>.</summary>
    public ApprovalState State { get; set; } = ApprovalState.Requested;

    /// <inheritdoc />
    public Task<Result> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public Task<ApprovalSummary> GetSummaryAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ApprovalSummary(State, State == ApprovalState.Requested ? 1 : 0, State == ApprovalState.Approved ? 1 : 0, State == ApprovalState.Rejected ? 1 : 0));

    /// <inheritdoc />
    public Task<Result> CancelAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default)
    {
        State = ApprovalState.Cancelled;
        return Task.FromResult(Result.Success());
    }
}
