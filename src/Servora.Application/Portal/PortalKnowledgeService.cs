using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Knowledge;
using Servora.Application.Knowledge.Markdown;
using Servora.Application.Security;
using Servora.Domain.Knowledge;

namespace Servora.Application.Portal;

/// <summary>
/// Default <see cref="IPortalKnowledgeService"/> implementation. Ranking (<see cref="KnowledgeRanker"/>) and rendering
/// (<see cref="SafeMarkdown"/>) are reused from the knowledge module; visibility is restricted to published articles.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
public sealed class PortalKnowledgeService(IAppDbContextFactory dbFactory, ICurrentUser currentUser, IClock clock) : IPortalKnowledgeService
{
    private const int CandidatesPerTerm = 100;
    private const int MaxQueryLength = 300;

    /// <inheritdoc />
    public async Task<Result<PortalKnowledgeHome>> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<PortalKnowledgeHome>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var lookups = await LoadLookupsAsync(db, cancellationToken);
        var visible = Visible(db);
        var popular = await visible.OrderByDescending(a => a.ViewCount).ThenBy(a => a.Title).Take(6).ToListAsync(cancellationToken);
        var newest = await visible.OrderByDescending(a => a.PublishedAt).Take(6).ToListAsync(cancellationToken);
        var count = await visible.CountAsync(cancellationToken);
        var perCategory = await visible.Where(a => a.CategoryId != null)
            .GroupBy(a => a.CategoryId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

        var categories = await db.Set<KnowledgeCategory>().AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Take(200)
            .ToListAsync(cancellationToken);

        return Result<PortalKnowledgeHome>.Success(new PortalKnowledgeHome
        {
            Categories = categories
                .Where(c => perCategory.ContainsKey(c.Id) && lookups.Bases.ContainsKey(c.KnowledgeBaseId))
                .Select(c => new PortalKnowledgeCategory(c.Id, c.Name, c.Description, c.Icon, lookups.Bases[c.KnowledgeBaseId], perCategory[c.Id]))
                .ToList(),
            Popular = popular.Select(a => ToSummary(a, lookups)).ToList(),
            Newest = newest.Select(a => ToSummary(a, lookups)).ToList(),
            ArticleCount = count,
        });
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PortalArticleSummary>>> SearchAsync(string? query, int take = 10, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<IReadOnlyList<PortalArticleSummary>>.Forbidden();
        }

        var text = query ?? string.Empty;
        if (text.Length > MaxQueryLength)
        {
            text = text[..MaxQueryLength];
        }

        var terms = KnowledgeRanker.Tokenize(text);
        if (terms.Count == 0)
        {
            return Result<IReadOnlyList<PortalArticleSummary>>.Success([]);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var visible = Visible(db);
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
            return Result<IReadOnlyList<PortalArticleSummary>>.Success([]);
        }

        var lookups = await LoadLookupsAsync(db, cancellationToken);
        var candidates = await visible.Where(a => candidateIds.Contains(a.Id)).ToListAsync(cancellationToken);
        IReadOnlyList<PortalArticleSummary> hits = candidates
            .Select(a =>
            {
                var plain = SafeMarkdown.ToPlainText(a.Body);
                var score = KnowledgeRanker.Score(new KnowledgeRankInput(a.Title, a.Summary, a.Keywords, plain, a.ViewCount, a.HelpfulCount, a.NotHelpfulCount), terms);
                return (Score: score, Summary: ToSummary(a, lookups) with { Snippet = KnowledgeRanker.Snippet(plain, terms) });
            })
            .Where(h => h.Score > 0)
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Summary.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(take, 1, 50))
            .Select(h => h.Summary)
            .ToList();
        return Result<IReadOnlyList<PortalArticleSummary>>.Success(hits);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PortalArticleSummary>>> ListByCategoryAsync(Guid categoryId, int take = 50, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<IReadOnlyList<PortalArticleSummary>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var lookups = await LoadLookupsAsync(db, cancellationToken);
        var articles = await Visible(db).Where(a => a.CategoryId == categoryId)
            .OrderByDescending(a => a.ViewCount).ThenBy(a => a.Title)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<PortalArticleSummary>>.Success(articles.Select(a => ToSummary(a, lookups)).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<PortalArticle>> GetArticleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<PortalArticle>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await Visible(db).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return Result<PortalArticle>.NotFound("The article was not found or is no longer published.");
        }

        var lookups = await LoadLookupsAsync(db, cancellationToken);
        bool? myVote = null;
        if (currentUser.UserId is { } me)
        {
            myVote = await db.Set<KnowledgeFeedback>().AsNoTracking()
                .Where(f => f.ArticleId == id && f.UserId == me)
                .Select(f => (bool?)f.Helpful)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var related = article.CategoryId is { } categoryId
            ? await Visible(db).Where(a => a.CategoryId == categoryId && a.Id != id).OrderByDescending(a => a.ViewCount).Take(5).ToListAsync(cancellationToken)
            : [];
        var rendered = SafeMarkdown.Render(article.Body);
        return Result<PortalArticle>.Success(new PortalArticle
        {
            Summary = ToSummary(article, lookups),
            SafeHtml = rendered.Html,
            Headings = rendered.Headings,
            AuthorName = article.AuthorName,
            UpdatedAt = article.UpdatedAt,
            HelpfulCount = article.HelpfulCount,
            NotHelpfulCount = article.NotHelpfulCount,
            MyVote = myVote,
            Related = related.Select(a => ToSummary(a, lookups)).ToList(),
        });
    }

    /// <inheritdoc />
    public async Task<Result> RecordViewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Counter updates bypass change tracking so they never conflict with concurrent edits (same as the knowledge module).
        var updated = await db.Set<KnowledgeArticle>()
            .Where(a => a.Id == id && a.State == KnowledgeArticleState.Published)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ViewCount, a => a.ViewCount + 1), cancellationToken);
        return updated == 0 ? Result.NotFound() : Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> VoteAsync(Guid id, bool helpful, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        if (currentUser.UserId is not { } me)
        {
            return Result.Invalid("Only signed-in users can rate articles.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visible(db).AnyAsync(a => a.Id == id, cancellationToken))
        {
            return Result.NotFound("Only published articles can be rated.");
        }

        var feedback = await db.Set<KnowledgeFeedback>().FirstOrDefaultAsync(f => f.ArticleId == id && f.UserId == me, cancellationToken);
        if (feedback is null)
        {
            db.Set<KnowledgeFeedback>().Add(new KnowledgeFeedback { ArticleId = id, UserId = me, Helpful = helpful, CreatedAt = clock.UtcNow });
        }
        else if (feedback.Helpful != helpful)
        {
            feedback.Helpful = helpful;
            feedback.CreatedAt = clock.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        var helpfulCount = await db.Set<KnowledgeFeedback>().CountAsync(f => f.ArticleId == id && f.Helpful, cancellationToken);
        var notHelpfulCount = await db.Set<KnowledgeFeedback>().CountAsync(f => f.ArticleId == id && !f.Helpful, cancellationToken);
        await db.Set<KnowledgeArticle>().Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.HelpfulCount, helpfulCount).SetProperty(a => a.NotHelpfulCount, notHelpfulCount), cancellationToken);
        return Result.Success();
    }

    private static PortalArticleSummary ToSummary(KnowledgeArticle article, KnowledgeNames lookups) => new()
    {
        Id = article.Id,
        Number = article.Number,
        Title = article.Title,
        Summary = article.Summary,
        KnowledgeBaseName = lookups.Bases.GetValueOrDefault(article.KnowledgeBaseId) ?? string.Empty,
        CategoryName = article.CategoryId is { } categoryId ? lookups.Categories.GetValueOrDefault(categoryId) : null,
        ViewCount = article.ViewCount,
        HelpfulPercent = article.HelpfulCount + article.NotHelpfulCount == 0
            ? null
            : (int)Math.Round(article.HelpfulCount * 100.0 / (article.HelpfulCount + article.NotHelpfulCount)),
        PublishedAt = article.PublishedAt,
    };

    private static async Task<KnowledgeNames> LoadLookupsAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var bases = await db.Set<KnowledgeBase>().AsNoTracking().Where(b => b.IsActive).Select(b => new { b.Id, b.Name }).Take(500)
            .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);
        var categories = await db.Set<KnowledgeCategory>().AsNoTracking().Select(c => new { c.Id, c.Name }).Take(2000)
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        return new KnowledgeNames(bases, categories);
    }

    private IQueryable<KnowledgeArticle> Visible(IAppDbContext db)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var activeBases = db.Set<KnowledgeBase>().Where(b => b.IsActive).Select(b => b.Id);
        return db.Set<KnowledgeArticle>().AsNoTracking()
            .Where(a => a.State == KnowledgeArticleState.Published && (a.ValidTo == null || a.ValidTo >= today) && activeBases.Contains(a.KnowledgeBaseId));
    }

    private async Task<bool> CanReadAsync(CancellationToken cancellationToken) =>
        await currentUser.HasPermissionAsync(Permissions.Portal.Access, cancellationToken)
        || await currentUser.HasPermissionAsync(Permissions.Knowledge.Read, cancellationToken);
}
