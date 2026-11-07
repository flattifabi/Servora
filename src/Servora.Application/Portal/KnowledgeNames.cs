namespace Servora.Application.Portal;

/// <summary>Display names of active knowledge bases and of categories, used to label portal article summaries.</summary>
/// <param name="Bases">Active knowledge base names by identifier.</param>
/// <param name="Categories">Category names by identifier.</param>
internal sealed record KnowledgeNames(IReadOnlyDictionary<Guid, string> Bases, IReadOnlyDictionary<Guid, string> Categories);
