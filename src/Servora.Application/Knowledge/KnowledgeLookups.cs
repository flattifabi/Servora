using Servora.Domain.Knowledge;

namespace Servora.Application.Knowledge;

/// <summary>Knowledge bases and categories loaded once per operation to resolve display names.</summary>
/// <param name="Bases">The knowledge bases.</param>
/// <param name="Categories">The categories.</param>
internal sealed record KnowledgeLookups(IReadOnlyList<KnowledgeBase> Bases, IReadOnlyList<KnowledgeCategory> Categories);
