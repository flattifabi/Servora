using Servora.Domain.Knowledge;

namespace Servora.Application.Knowledge;

/// <summary>A version snapshot of an article.</summary>
/// <param name="Id">The snapshot identifier.</param>
/// <param name="VersionNumber">The content version number.</param>
/// <param name="Title">The title at that version.</param>
/// <param name="State">The state when the snapshot was taken.</param>
/// <param name="ChangeNote">The change note.</param>
/// <param name="CreatedBy">The editor.</param>
/// <param name="CreatedAt">The snapshot time.</param>
public sealed record KnowledgeVersionDto(Guid Id, int VersionNumber, string Title, KnowledgeArticleState State, string? ChangeNote, string? CreatedBy, DateTimeOffset CreatedAt);
