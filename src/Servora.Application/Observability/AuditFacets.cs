namespace Servora.Application.Observability;

/// <summary>Distinct values for the audit viewer filter drop-downs.</summary>
/// <param name="Categories">The categories.</param>
/// <param name="Actions">The actions.</param>
public sealed record AuditFacets(IReadOnlyList<string> Categories, IReadOnlyList<string> Actions);
