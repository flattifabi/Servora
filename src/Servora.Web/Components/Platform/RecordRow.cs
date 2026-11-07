using Servora.Application.Conditions;

namespace Servora.Web.Components.Platform;

/// <summary>A record row of the generic list view (wraps the field dictionary returned by the record service).</summary>
/// <param name="Values">The field values including <c>Field.display</c> entries.</param>
public sealed record RecordRow(IReadOnlyDictionary<string, object?> Values)
{
    /// <summary>Gets the record identifier.</summary>
    public Guid Id => ConditionValueConverter.TryGetGuid(Values.GetValueOrDefault("Id"), out var id) ? id : Guid.Empty;
}
