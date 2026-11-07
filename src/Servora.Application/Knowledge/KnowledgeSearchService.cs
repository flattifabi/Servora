using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Knowledge.Markdown;
using Servora.Application.Security;
using Servora.Domain.Knowledge;

namespace Servora.Application.Knowledge;

/// <summary>
/// Default <see cref="IKnowledgeSearchService"/>: provider-neutral candidate retrieval (case-insensitive <c>Contains</c> per term,
/// bounded) followed by in-memory ranking with <see cref="KnowledgeRanker"/>.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
public sealed class KnowledgeSearchService(IAppDbContextFactory dbFactory, ICurrentUser currentUser, IClock clock) : IKnowledgeSearchService
{
    private const int CandidatesPerTerm = 100;
    private const int MaxTake = 50;
    private const int MaxQueryLength = 300;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<KnowledgeSearchHit>>> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Knowledge.Read, cancellationToken))
        {
            return Result<IReadOnlyList<KnowledgeSearchHit>>.Forbidden();
        }

        var query = request.Query ?? string.Empty;
        if (query.Length > MaxQueryLength)
        {
            query = query[..MaxQueryLength];
        }

        var terms = KnowledgeRanker.Tokenize(query);
        if (terms.Count == 0)
        {
            return Result<IReadOnlyList<KnowledgeSearchHit>>.Success([]);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var visible = db.Set<KnowledgeArticle>().AsNoTracking()
            .Where(a => a.State == KnowledgeArticleState.Published && (a.ValidTo == null || a.ValidTo >= today));
        if (request.KnowledgeBaseId is { } baseId)
        {
            visible = visible.Where(a => a.KnowledgeBaseId == baseId);
        }

        if (request.CategoryId is { } categoryId)
        {
            visible = visible.Where(a => a.CategoryId == categoryId);
        }

        var candidateIds = new HashSet<Guid>();
        foreach (var term in terms)
        {
            var ids = await visible
                .Where(a => a.Title.ToLower().Contains(term) || (a.Summary != null && a.Summary.ToLower().Contains(term))
                    || (a.Keywords != null && a.Keywords.ToLower().Contains(term)) || a.Body.ToLower().Contains(term))
                .OrderByDescending(a => a.ViewCount)
                .Select(a => a.Id)
                .Take(CandidatesPerTerm)
                .ToListAsync(cancellationToken);
            candidateIds.UnionWith(ids);
        }

        if (candidateIds.Count == 0)
        {
            return Result<IReadOnlyList<KnowledgeSearchHit>>.Success([]);
        }

        var candidates = await db.Set<KnowledgeArticle>().AsNoTracking().Where(a => candidateIds.Contains(a.Id)).ToListAsync(cancellationToken);
        var baseNames = await db.Set<KnowledgeBase>().AsNoTracking().Select(b => new { b.Id, b.Name }).Take(500).ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);
        var categoryNames = await db.Set<KnowledgeCategory>().AsNoTracking().Select(c => new { c.Id, c.Name }).Take(2000).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var take = Math.Clamp(request.Take, 1, MaxTake);
        var hits = candidates
            .Select(a =>
            {
                var plain = SafeMarkdown.ToPlainText(a.Body);
                var score = KnowledgeRanker.Score(new KnowledgeRankInput(a.Title, a.Summary, a.Keywords, plain, a.ViewCount, a.HelpfulCount, a.NotHelpfulCount), terms);
                return new KnowledgeSearchHit(
                    a.Id,
                    a.Number,
                    a.Title,
                    a.Summary,
                    KnowledgeRanker.Snippet(plain, terms),
                    score,
                    baseNames.GetValueOrDefault(a.KnowledgeBaseId) ?? string.Empty,
                    a.CategoryId is { } c ? categoryNames.GetValueOrDefault(c) : null,
                    a.ViewCount,
                    a.HelpfulCount,
                    a.PublishedAt);
            })
            .Where(h => h.Score > 0)
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Title, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();

        return Result<IReadOnlyList<KnowledgeSearchHit>>.Success(hits);
    }
}
