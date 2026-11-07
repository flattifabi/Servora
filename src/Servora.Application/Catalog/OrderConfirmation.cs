using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>Result of a successful catalog order.</summary>
/// <param name="RequestId">The request identifier.</param>
/// <param name="RequestNumber">The request number (REQ…).</param>
/// <param name="RequestItemId">The requested item identifier.</param>
/// <param name="RequestItemNumber">The requested item number (RITM…).</param>
/// <param name="ApprovalRequired">Whether the item waits for approval.</param>
/// <param name="State">The state of the requested item after ordering.</param>
public sealed record OrderConfirmation(Guid RequestId, string RequestNumber, Guid RequestItemId, string RequestItemNumber, bool ApprovalRequired, RequestItemState State);
