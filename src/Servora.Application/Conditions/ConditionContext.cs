using Servora.Application.Events;

namespace Servora.Application.Conditions;

/// <summary>Optional evaluation context for change-aware operators and <see cref="ConditionOperator.IsMe"/>.</summary>
/// <param name="CurrentUserId">The current user for <see cref="ConditionOperator.IsMe"/>.</param>
/// <param name="Changes">The changes of the record event, for <c>Changed*</c> operators.</param>
public sealed record ConditionContext(Guid? CurrentUserId = null, IReadOnlyList<RecordFieldChange>? Changes = null);
