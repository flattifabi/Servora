using Servora.Application.Common;

namespace Servora.Application.Catalog;

/// <summary>Request fulfilment use cases for fulfillers (<c>request.read</c> / <c>request.write</c>) and requesters (own requests).</summary>
public interface IRequestService
{
    /// <summary>Lists requests (<c>request.read</c>).</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="page">Paging, sorting (<c>Number</c>, <c>State</c>, <c>Opened</c>) and search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of requests.</returns>
    Task<Result<PagedResult<RequestListItem>>> ListRequestsAsync(RequestListFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Lists requested items (<c>request.read</c>).</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="page">Paging, sorting and search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of requested items.</returns>
    Task<Result<PagedResult<RequestItemListItem>>> ListItemsAsync(RequestListFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Lists catalog tasks (<c>request.read</c>).</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="page">Paging, sorting and search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of catalog tasks.</returns>
    Task<Result<PagedResult<CatalogTaskListItem>>> ListTasksAsync(RequestListFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Gets key figures (<c>request.read</c>).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The statistics.</returns>
    Task<Result<RequestStats>> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists the current user's own requests (requested for or opened by the user).</summary>
    /// <param name="page">Paging and search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of requests.</returns>
    Task<Result<PagedResult<RequestListItem>>> ListMyRequestsAsync(PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Gets a request with items, answers and tasks (<c>request.read</c> or own request).</summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The request.</returns>
    Task<Result<RequestDetail>> GetAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Updates a catalog task and rolls the result up to the requested item and request (<c>request.write</c>).</summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="update">The changes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateTaskAsync(Guid taskId, CatalogTaskUpdate update, CancellationToken cancellationToken = default);

    /// <summary>Assigns a catalog task to the current user and starts work (<c>request.write</c>).</summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> AssignTaskToMeAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>Cancels an open request (<c>request.write</c>, or the requester while the request is not closed).</summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="reason">The reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> CancelRequestAsync(Guid requestId, string reason, CancellationToken cancellationToken = default);
}
