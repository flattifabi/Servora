using Microsoft.EntityFrameworkCore;
using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Application.Directory;
using Servora.Application.Knowledge.Markdown;
using Servora.Application.Observability;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Domain.Cmdb;
using Servora.Domain.Common;
using Servora.Domain.Knowledge;

namespace Servora.Application.Knowledge;

/// <summary>Default <see cref="IKnowledgeService"/> implementation.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="numbers">The number sequence service.</param>
/// <param name="activity">The activity stream.</param>
/// <param name="audit">The audit log.</param>
/// <param name="directory">The directory look-ups.</param>
/// <param name="clock">The clock.</param>
public sealed class KnowledgeService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    INumberSequenceService numbers,
    IActivityService activity,
    IAuditService audit,
    IDirectoryLookupService directory,
    IClock clock) : IKnowledgeService
{
    private const string NumberPrefix = "KB";
    private const string AuditCategory = "knowledge";
    private const int MaxTitleLength = 250;
    private const int MaxSummaryLength = 1000;
    private const int MaxBodyLength = 100_000;
    private const int MaxKeywordsLength = 500;
    private const int MaxCommentLength = 2000;

    /// <inheritdoc />
    public async Task<Result<KnowledgeHomeData>> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<KnowledgeHomeData>.Forbidden();
        }

        var isAuthor = await CanSeeUnpublishedAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var lookups = await LoadLookupsAsync(db, cancellationToken);
        var published = VisibleToReaders(db);

        var popular = await published.OrderByDescending(a => a.ViewCount).ThenBy(a => a.Title).Take(6).ToListAsync(cancellationToken);
        var newest = await published.OrderByDescending(a => a.PublishedAt).Take(6).ToListAsync(cancellationToken);
        var publishedCount = await published.CountAsync(cancellationToken);
        var totalViews = publishedCount == 0 ? 0 : await published.SumAsync(a => a.ViewCount, cancellationToken);
        var helpful = publishedCount == 0 ? 0 : await published.SumAsync(a => a.HelpfulCount, cancellationToken);
        var notHelpful = publishedCount == 0 ? 0 : await published.SumAsync(a => a.NotHelpfulCount, cancellationToken);
        var perBase = await published.GroupBy(a => a.KnowledgeBaseId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var perCategory = await published.Where(a => a.CategoryId != null).GroupBy(a => a.CategoryId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

        var data = new KnowledgeHomeData
        {
            Bases = lookups.Bases.Where(b => b.IsActive).Select(b => ToDto(b, perBase.GetValueOrDefault(b.Id))).ToList(),
            Categories = lookups.Categories.Where(c => c.IsActive && lookups.Bases.Any(b => b.Id == c.KnowledgeBaseId && b.IsActive))
                .Select(c => ToDto(c, perCategory.GetValueOrDefault(c.Id))).ToList(),
            Popular = popular.Select(a => ToSummary(a, lookups)).ToList(),
            Newest = newest.Select(a => ToSummary(a, lookups)).ToList(),
            PublishedCount = publishedCount,
            TotalViews = totalViews,
            HelpfulPercent = helpful + notHelpful == 0 ? null : (int)Math.Round(helpful * 100.0 / (helpful + notHelpful)),
        };

        if (isAuthor)
        {
            data.DraftCount = await db.Set<KnowledgeArticle>().CountAsync(a => a.State == KnowledgeArticleState.Draft, cancellationToken);
            data.ReviewCount = await db.Set<KnowledgeArticle>().CountAsync(a => a.State == KnowledgeArticleState.Review, cancellationToken);
        }

        return Result<KnowledgeHomeData>.Success(data);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<KnowledgeBaseDto>>> GetBasesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<IReadOnlyList<KnowledgeBaseDto>>.Forbidden();
        }

        includeInactive = includeInactive && await currentUser.HasPermissionAsync(Permissions.Knowledge.Manage, cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var lookups = await LoadLookupsAsync(db, cancellationToken);
        var counts = await VisibleToReaders(db).GroupBy(a => a.KnowledgeBaseId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        return Result<IReadOnlyList<KnowledgeBaseDto>>.Success(lookups.Bases.Where(b => includeInactive || b.IsActive)
            .Select(b => ToDto(b, counts.GetValueOrDefault(b.Id))).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<KnowledgeCategoryDto>>> GetCategoriesAsync(Guid? knowledgeBaseId = null, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<IReadOnlyList<KnowledgeCategoryDto>>.Forbidden();
        }

        includeInactive = includeInactive && await currentUser.HasPermissionAsync(Permissions.Knowledge.Manage, cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var lookups = await LoadLookupsAsync(db, cancellationToken);
        var counts = await VisibleToReaders(db).Where(a => a.CategoryId != null).GroupBy(a => a.CategoryId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        return Result<IReadOnlyList<KnowledgeCategoryDto>>.Success(lookups.Categories
            .Where(c => (includeInactive || c.IsActive) && (knowledgeBaseId is null || c.KnowledgeBaseId == knowledgeBaseId))
            .Select(c => ToDto(c, counts.GetValueOrDefault(c.Id))).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveBaseAsync(Guid? id, KnowledgeBaseInput input, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Knowledge.Manage, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 150)
        {
            return Result<Guid>.Invalid("The name is required (max 150 characters).");
        }

        if (input.Description?.Length > 1000)
        {
            return Result<Guid>.Invalid("The description must not exceed 1000 characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Set<KnowledgeBase>().AnyAsync(b => b.Name == name && b.Id != id, cancellationToken))
        {
            return Result<Guid>.Conflict("A knowledge base with this name already exists.");
        }

        KnowledgeBase? entity;
        if (id is { } existingId)
        {
            entity = await db.Set<KnowledgeBase>().FirstOrDefaultAsync(b => b.Id == existingId, cancellationToken);
            if (entity is null)
            {
                return Result<Guid>.NotFound();
            }
        }
        else
        {
            entity = new KnowledgeBase();
            db.Set<KnowledgeBase>().Add(entity);
        }

        entity.Name = name;
        entity.Description = Clean(input.Description);
        entity.Icon = CleanIcon(input.Icon, "book-open");
        entity.IsActive = input.IsActive;
        entity.SortOrder = input.SortOrder;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry(AuditCategory, id is null ? "base.create" : "base.update") { TargetType = KnowledgeTables.KnowledgeBase, TargetId = entity.Id.ToString(), TargetName = entity.Name }, cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Knowledge.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Set<KnowledgeBase>().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.NotFound();
        }

        if (await db.Set<KnowledgeArticle>().AnyAsync(a => a.KnowledgeBaseId == id, cancellationToken)
            || await db.Set<KnowledgeCategory>().AnyAsync(c => c.KnowledgeBaseId == id, cancellationToken))
        {
            return Result.Conflict("The knowledge base still contains categories or articles. Move or delete them first, or deactivate the knowledge base.");
        }

        db.Set<KnowledgeBase>().Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry(AuditCategory, "base.delete") { TargetType = KnowledgeTables.KnowledgeBase, TargetId = id.ToString(), TargetName = entity.Name }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveCategoryAsync(Guid? id, KnowledgeCategoryInput input, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Knowledge.Manage, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 150)
        {
            return Result<Guid>.Invalid("The name is required (max 150 characters).");
        }

        if (input.Description?.Length > 1000)
        {
            return Result<Guid>.Invalid("The description must not exceed 1000 characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Set<KnowledgeBase>().AnyAsync(b => b.Id == input.KnowledgeBaseId, cancellationToken))
        {
            return Result<Guid>.Invalid("Select a knowledge base.");
        }

        if (await db.Set<KnowledgeCategory>().AnyAsync(c => c.KnowledgeBaseId == input.KnowledgeBaseId && c.Name == name && c.Id != id, cancellationToken))
        {
            return Result<Guid>.Conflict("A category with this name already exists in the knowledge base.");
        }

        KnowledgeCategory? entity;
        if (id is { } existingId)
        {
            entity = await db.Set<KnowledgeCategory>().FirstOrDefaultAsync(c => c.Id == existingId, cancellationToken);
            if (entity is null)
            {
                return Result<Guid>.NotFound();
            }

            if (entity.KnowledgeBaseId != input.KnowledgeBaseId)
            {
                // Articles follow their category into the new knowledge base.
                var articles = await db.Set<KnowledgeArticle>().Where(a => a.CategoryId == existingId).Take(5000).ToListAsync(cancellationToken);
                articles.ForEach(a => a.KnowledgeBaseId = input.KnowledgeBaseId);
            }
        }
        else
        {
            entity = new KnowledgeCategory();
            db.Set<KnowledgeCategory>().Add(entity);
        }

        entity.KnowledgeBaseId = input.KnowledgeBaseId;
        entity.Name = name;
        entity.Description = Clean(input.Description);
        entity.Icon = CleanIcon(input.Icon, "folder");
        entity.SortOrder = input.SortOrder;
        entity.IsActive = input.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry(AuditCategory, id is null ? "category.create" : "category.update") { TargetType = KnowledgeTables.Category, TargetId = entity.Id.ToString(), TargetName = entity.Name }, cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Knowledge.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Set<KnowledgeCategory>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.NotFound();
        }

        var articles = await db.Set<KnowledgeArticle>().Where(a => a.CategoryId == id).Take(5000).ToListAsync(cancellationToken);
        articles.ForEach(a => a.CategoryId = null);
        db.Set<KnowledgeCategory>().Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry(AuditCategory, "category.delete") { TargetType = KnowledgeTables.Category, TargetId = id.ToString(), TargetName = entity.Name }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<KnowledgeArticleSummary>>> ListAsync(KnowledgeArticleQuery query, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<PagedResult<KnowledgeArticleSummary>>.Forbidden();
        }

        var seeAll = await CanSeeUnpublishedAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var lookups = await LoadLookupsAsync(db, cancellationToken);
        var articles = seeAll ? db.Set<KnowledgeArticle>().AsNoTracking() : VisibleToReaders(db);
        if (query.State is { } state)
        {
            articles = articles.Where(a => a.State == state);
        }

        if (query.KnowledgeBaseId is { } baseId)
        {
            articles = articles.Where(a => a.KnowledgeBaseId == baseId);
        }

        if (query.CategoryId is { } categoryId)
        {
            articles = articles.Where(a => a.CategoryId == categoryId);
        }

        if (query.MineOnly)
        {
            var userId = currentUser.UserId;
            articles = articles.Where(a => a.AuthorId == userId);
        }

        if (!string.IsNullOrWhiteSpace(query.Page.Search))
        {
            var term = query.Page.Search.Trim().ToLower();
            articles = articles.Where(a => a.Number.ToLower().Contains(term) || a.Title.ToLower().Contains(term) || (a.Summary != null && a.Summary.ToLower().Contains(term)));
        }

        var desc = query.Page.SortDescending;
        articles = query.Page.SortBy switch
        {
            "Number" => desc ? articles.OrderByDescending(a => a.Number) : articles.OrderBy(a => a.Number),
            "Title" => desc ? articles.OrderByDescending(a => a.Title) : articles.OrderBy(a => a.Title),
            "State" => desc ? articles.OrderByDescending(a => a.State) : articles.OrderBy(a => a.State),
            "ViewCount" => desc ? articles.OrderByDescending(a => a.ViewCount) : articles.OrderBy(a => a.ViewCount),
            "HelpfulCount" => desc ? articles.OrderByDescending(a => a.HelpfulCount) : articles.OrderBy(a => a.HelpfulCount),
            "UpdatedAt" => desc ? articles.OrderByDescending(a => a.UpdatedAt) : articles.OrderBy(a => a.UpdatedAt),
            _ => articles.OrderByDescending(a => a.UpdatedAt),
        };

        var page = await articles.ToPagedResultAsync(query.Page, cancellationToken);
        return Result<PagedResult<KnowledgeArticleSummary>>.Success(
            new PagedResult<KnowledgeArticleSummary>(page.Items.Select(a => ToSummary(a, lookups)).ToList(), page.TotalCount, page.Page, page.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<KnowledgeArticleDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<KnowledgeArticleDetail>.Forbidden();
        }

        var canWrite = await currentUser.HasPermissionAsync(Permissions.Knowledge.Write, cancellationToken);
        var canPublish = await currentUser.HasPermissionAsync(Permissions.Knowledge.Publish, cancellationToken);
        var canManage = await currentUser.HasPermissionAsync(Permissions.Knowledge.Manage, cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.Set<KnowledgeArticle>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null || (!(canWrite || canPublish) && !IsVisibleToReaders(article)))
        {
            return Result<KnowledgeArticleDetail>.NotFound("The knowledge article was not found or is not published.");
        }

        var lookups = await LoadLookupsAsync(db, cancellationToken);
        var ciIds = await db.Set<KnowledgeArticleCi>().AsNoTracking().Where(l => l.ArticleId == id).Select(l => l.ConfigurationItemId).Take(100).ToListAsync(cancellationToken);
        var cis = await directory.GetConfigurationItemsAsync(ciIds, cancellationToken);
        bool? myVote = null;
        if (currentUser.UserId is { } userId)
        {
            myVote = await db.Set<KnowledgeFeedback>().AsNoTracking().Where(f => f.ArticleId == id && f.UserId == userId).Select(f => (bool?)f.Helpful).FirstOrDefaultAsync(cancellationToken);
        }

        var rendered = SafeMarkdown.Render(article.Body);
        var detail = new KnowledgeArticleDetail
        {
            Id = article.Id,
            Number = article.Number,
            Title = article.Title,
            Summary = article.Summary,
            Body = article.Body,
            Html = rendered.Html,
            Headings = rendered.Headings,
            KnowledgeBaseId = article.KnowledgeBaseId,
            KnowledgeBaseName = lookups.Bases.FirstOrDefault(b => b.Id == article.KnowledgeBaseId)?.Name ?? string.Empty,
            CategoryId = article.CategoryId,
            CategoryName = lookups.Categories.FirstOrDefault(c => c.Id == article.CategoryId)?.Name,
            State = article.State,
            AuthorName = article.AuthorName,
            ReviewComment = canWrite || canPublish ? article.ReviewComment : null,
            PublishedAt = article.PublishedAt,
            RetiredAt = article.RetiredAt,
            ValidTo = article.ValidTo,
            Keywords = article.Keywords,
            ArticleVersion = article.ArticleVersion,
            ViewCount = article.ViewCount,
            HelpfulCount = article.HelpfulCount,
            NotHelpfulCount = article.NotHelpfulCount,
            CreatedAt = article.CreatedAt,
            UpdatedAt = article.UpdatedAt,
            UpdatedBy = article.UpdatedBy,
            Version = article.Version,
            RelatedCis = ciIds.Where(cis.ContainsKey).Select(c => cis[c]).OrderBy(c => c.Display).ToList(),
            MyVote = myVote,
            CanEdit = canWrite && KnowledgeWorkflow.IsEditable(article.State),
            CanDelete = canManage,
            AllowedActions = KnowledgeWorkflow.AllowedActions(article.State)
                .Where(a => KnowledgeWorkflow.RequiresReviewer(a) ? canPublish : canWrite)
                .ToList(),
        };

        return Result<KnowledgeArticleDetail>.Success(detail);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(KnowledgeArticleInput input, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Knowledge.Write, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var validation = await ValidateAsync(db, input, cancellationToken);
        if (validation is not null)
        {
            return Result<Guid>.Invalid(validation);
        }

        var article = new KnowledgeArticle
        {
            Number = await numbers.NextAsync(TableNames.KnowledgeArticle, NumberPrefix, cancellationToken),
            State = KnowledgeArticleState.Draft,
            AuthorId = currentUser.UserId,
            AuthorName = currentUser.DisplayName,
            ArticleVersion = 1,
        };
        Apply(article, input);
        db.Set<KnowledgeArticle>().Add(article);
        db.Set<KnowledgeArticleVersion>().Add(Snapshot(article, Clean(input.ChangeNote) ?? "Created"));
        await db.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(article.Id);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(Guid id, KnowledgeArticleInput input, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Knowledge.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.Set<KnowledgeArticle>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return Result.NotFound();
        }

        if (!KnowledgeWorkflow.IsEditable(article.State))
        {
            return Result.Invalid("Published and retired articles cannot be edited. Create a new revision first.");
        }

        if (input.Version != 0 && input.Version != article.Version)
        {
            return Result.Conflict("The article was changed by someone else. Reload it and apply your changes again.");
        }

        var validation = await ValidateAsync(db, input, cancellationToken);
        if (validation is not null)
        {
            return Result.Invalid(validation);
        }

        var contentChanged = article.Title != input.Title.Trim() || article.Summary != Clean(input.Summary) || article.Body != (input.Body ?? string.Empty);
        Apply(article, input);
        if (contentChanged)
        {
            article.ArticleVersion++;
            db.Set<KnowledgeArticleVersion>().Add(Snapshot(article, Clean(input.ChangeNote)));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("The article was changed by someone else. Reload it and apply your changes again.");
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> TransitionAsync(Guid id, KnowledgeWorkflowAction action, string? comment = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.Set<KnowledgeArticle>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return await CanReadAsync(cancellationToken) ? Result.NotFound() : Result.Forbidden();
        }

        var requiresReviewer = KnowledgeWorkflow.RequiresReviewer(action);
        if (!await currentUser.HasPermissionAsync(requiresReviewer ? Permissions.Knowledge.Publish : Permissions.Knowledge.Write, cancellationToken))
        {
            return Result.Forbidden(requiresReviewer ? "Only knowledge reviewers can publish, reject or retire articles." : "You do not have permission to change this article.");
        }

        var from = article.State;
        if (KnowledgeWorkflow.TargetOf(from, action) is not { } target)
        {
            return Result.Invalid($"'{KnowledgeWorkflow.Label(action)}' is not possible while the article is in state {KnowledgeWorkflow.Label(from)}.");
        }

        comment = Clean(comment);
        if (comment?.Length > MaxCommentLength)
        {
            return Result.Invalid($"The comment must not exceed {MaxCommentLength} characters.");
        }

        if (action == KnowledgeWorkflowAction.Reject && comment is null)
        {
            return Result.Invalid("Explain to the author what needs to change.");
        }

        var now = clock.UtcNow;
        article.State = target;
        switch (action)
        {
            case KnowledgeWorkflowAction.Publish:
                article.PublishedAt = now;
                article.RetiredAt = null;
                article.ReviewedById = currentUser.UserId;
                article.ReviewComment = comment;
                break;
            case KnowledgeWorkflowAction.Reject:
                article.ReviewedById = currentUser.UserId;
                article.ReviewComment = comment;
                break;
            case KnowledgeWorkflowAction.Retire:
                article.RetiredAt = now;
                break;
            case KnowledgeWorkflowAction.SubmitForReview:
                article.ReviewComment = null;
                break;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("The article was changed by someone else. Reload it and try again.");
        }

        var message = $"{KnowledgeWorkflow.Label(action)}: {KnowledgeWorkflow.Label(from)} → {KnowledgeWorkflow.Label(target)}" + (comment is null ? string.Empty : $" — {comment}");
        await activity.AddSystemAsync(TableNames.KnowledgeArticle, article.Id, message, cancellationToken);
        if (action is KnowledgeWorkflowAction.Publish or KnowledgeWorkflowAction.Retire)
        {
            await audit.WriteAsync(new AuditEntry(AuditCategory, action == KnowledgeWorkflowAction.Publish ? "article.publish" : "article.retire")
            {
                TargetType = TableNames.KnowledgeArticle,
                TargetId = article.Id.ToString(),
                TargetName = article.Number,
                Message = article.Title,
            }, cancellationToken);
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Knowledge.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.Set<KnowledgeArticle>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return Result.NotFound();
        }

        db.Set<KnowledgeArticleVersion>().RemoveRange(await db.Set<KnowledgeArticleVersion>().Where(v => v.ArticleId == id).ToListAsync(cancellationToken));
        db.Set<KnowledgeFeedback>().RemoveRange(await db.Set<KnowledgeFeedback>().Where(f => f.ArticleId == id).ToListAsync(cancellationToken));
        db.Set<KnowledgeArticleCi>().RemoveRange(await db.Set<KnowledgeArticleCi>().Where(l => l.ArticleId == id).ToListAsync(cancellationToken));
        db.Set<KnowledgeArticle>().Remove(article);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry(AuditCategory, "article.delete") { TargetType = TableNames.KnowledgeArticle, TargetId = id.ToString(), TargetName = article.Number, Message = article.Title }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> RecordViewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Counter updates bypass change tracking so that they never conflict with concurrent edits (no version bump).
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

        if (currentUser.UserId is not { } userId)
        {
            return Result.Invalid("Only signed-in users can rate articles.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.Set<KnowledgeArticle>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null || !IsVisibleToReaders(article))
        {
            return Result.NotFound("Only published articles can be rated.");
        }

        var feedback = await db.Set<KnowledgeFeedback>().FirstOrDefaultAsync(f => f.ArticleId == id && f.UserId == userId, cancellationToken);
        if (feedback is null)
        {
            db.Set<KnowledgeFeedback>().Add(new KnowledgeFeedback { ArticleId = id, UserId = userId, Helpful = helpful, CreatedAt = clock.UtcNow });
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

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<KnowledgeVersionDto>>> GetVersionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanSeeUnpublishedAsync(cancellationToken))
        {
            return Result<IReadOnlyList<KnowledgeVersionDto>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var versions = await db.Set<KnowledgeArticleVersion>().AsNoTracking().Where(v => v.ArticleId == id)
            .OrderByDescending(v => v.VersionNumber).Take(200)
            .Select(v => new KnowledgeVersionDto(v.Id, v.VersionNumber, v.Title, v.State, v.ChangeNote, v.CreatedBy, v.CreatedAt))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<KnowledgeVersionDto>>.Success(versions);
    }

    /// <inheritdoc />
    public async Task<Result<string>> GetVersionBodyAsync(Guid articleId, Guid versionId, CancellationToken cancellationToken = default)
    {
        if (!await CanSeeUnpublishedAsync(cancellationToken))
        {
            return Result<string>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var body = await db.Set<KnowledgeArticleVersion>().AsNoTracking().Where(v => v.Id == versionId && v.ArticleId == articleId).Select(v => v.Body).FirstOrDefaultAsync(cancellationToken);
        return body is null ? Result<string>.NotFound() : Result<string>.Success(body);
    }

    /// <inheritdoc />
    public async Task<Result> AddRelatedCiAsync(Guid id, Guid configurationItemId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Knowledge.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Set<KnowledgeArticle>().AnyAsync(a => a.Id == id, cancellationToken))
        {
            return Result.NotFound();
        }

        if (!await db.Set<ConfigurationItem>().AnyAsync(c => c.Id == configurationItemId, cancellationToken))
        {
            return Result.Invalid("The configuration item does not exist.");
        }

        if (await db.Set<KnowledgeArticleCi>().AnyAsync(l => l.ArticleId == id && l.ConfigurationItemId == configurationItemId, cancellationToken))
        {
            return Result.Success();
        }

        db.Set<KnowledgeArticleCi>().Add(new KnowledgeArticleCi { ArticleId = id, ConfigurationItemId = configurationItemId });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> RemoveRelatedCiAsync(Guid id, Guid configurationItemId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Knowledge.Write, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var link = await db.Set<KnowledgeArticleCi>().FirstOrDefaultAsync(l => l.ArticleId == id && l.ConfigurationItemId == configurationItemId, cancellationToken);
        if (link is null)
        {
            return Result.NotFound();
        }

        db.Set<KnowledgeArticleCi>().Remove(link);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<KnowledgeArticleSummary>>> GetForConfigurationItemAsync(Guid configurationItemId, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(cancellationToken))
        {
            return Result<IReadOnlyList<KnowledgeArticleSummary>>.Forbidden();
        }

        var seeAll = await CanSeeUnpublishedAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var lookups = await LoadLookupsAsync(db, cancellationToken);
        var articleIds = db.Set<KnowledgeArticleCi>().Where(l => l.ConfigurationItemId == configurationItemId).Select(l => l.ArticleId);
        var source = seeAll ? db.Set<KnowledgeArticle>().AsNoTracking() : VisibleToReaders(db);
        var articles = await source.Where(a => articleIds.Contains(a.Id)).OrderBy(a => a.Title).Take(50).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<KnowledgeArticleSummary>>.Success(articles.Select(a => ToSummary(a, lookups)).ToList());
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string CleanIcon(string? icon, string fallback)
    {
        var value = icon?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(value) || value.Length > 64 || !value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-') ? fallback : value;
    }

    private static KnowledgeBaseDto ToDto(KnowledgeBase b, int count) => new(b.Id, b.Name, b.Description, b.Icon, b.IsActive, b.SortOrder, count, b.Version);

    private static KnowledgeCategoryDto ToDto(KnowledgeCategory c, int count) => new(c.Id, c.KnowledgeBaseId, c.Name, c.Description, c.Icon, c.SortOrder, c.IsActive, count, c.Version);

    private static KnowledgeArticleSummary ToSummary(KnowledgeArticle a, KnowledgeLookups lookups) => new(
        a.Id,
        a.Number,
        a.Title,
        a.Summary,
        a.State,
        a.KnowledgeBaseId,
        lookups.Bases.FirstOrDefault(b => b.Id == a.KnowledgeBaseId)?.Name ?? string.Empty,
        a.CategoryId,
        lookups.Categories.FirstOrDefault(c => c.Id == a.CategoryId)?.Name,
        a.AuthorName,
        a.ViewCount,
        a.HelpfulCount,
        a.NotHelpfulCount,
        a.UpdatedAt,
        a.PublishedAt,
        a.ArticleVersion);

    private static async Task<KnowledgeLookups> LoadLookupsAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var bases = await db.Set<KnowledgeBase>().AsNoTracking().OrderBy(b => b.SortOrder).ThenBy(b => b.Name).Take(500).ToListAsync(cancellationToken);
        var categories = await db.Set<KnowledgeCategory>().AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Take(2000).ToListAsync(cancellationToken);
        return new KnowledgeLookups(bases, categories);
    }

    private static void Apply(KnowledgeArticle article, KnowledgeArticleInput input)
    {
        article.Title = input.Title.Trim();
        article.Summary = Clean(input.Summary);
        article.Body = input.Body ?? string.Empty;
        article.KnowledgeBaseId = input.KnowledgeBaseId;
        article.CategoryId = input.CategoryId;
        article.ValidTo = input.ValidTo;
        article.Keywords = Clean(input.Keywords);
    }

    private KnowledgeArticleVersion Snapshot(KnowledgeArticle article, string? note) => new()
    {
        ArticleId = article.Id,
        VersionNumber = article.ArticleVersion,
        Title = article.Title,
        Summary = article.Summary,
        Body = article.Body,
        State = article.State,
        ChangeNote = note?.Length > 500 ? note[..500] : note,
        CreatedBy = currentUser.DisplayName,
        CreatedAt = clock.UtcNow,
    };

    private static async Task<string?> ValidateAsync(IAppDbContext db, KnowledgeArticleInput input, CancellationToken cancellationToken)
    {
        var title = input.Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
        {
            return "The title is required.";
        }

        if (title.Length > MaxTitleLength)
        {
            return $"The title must not exceed {MaxTitleLength} characters.";
        }

        if (input.Summary?.Length > MaxSummaryLength)
        {
            return $"The summary must not exceed {MaxSummaryLength} characters.";
        }

        if ((input.Body ?? string.Empty).Length > MaxBodyLength)
        {
            return $"The article body must not exceed {MaxBodyLength:N0} characters.";
        }

        if (input.Keywords?.Length > MaxKeywordsLength)
        {
            return $"The keywords must not exceed {MaxKeywordsLength} characters.";
        }

        if (!await db.Set<KnowledgeBase>().AnyAsync(b => b.Id == input.KnowledgeBaseId, cancellationToken))
        {
            return "Select a knowledge base.";
        }

        if (input.CategoryId is { } categoryId
            && !await db.Set<KnowledgeCategory>().AnyAsync(c => c.Id == categoryId && c.KnowledgeBaseId == input.KnowledgeBaseId, cancellationToken))
        {
            return "The category does not belong to the selected knowledge base.";
        }

        return null;
    }

    private IQueryable<KnowledgeArticle> VisibleToReaders(IAppDbContext db)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        return db.Set<KnowledgeArticle>().AsNoTracking().Where(a => a.State == KnowledgeArticleState.Published && (a.ValidTo == null || a.ValidTo >= today));
    }

    private bool IsVisibleToReaders(KnowledgeArticle article) =>
        article.State == KnowledgeArticleState.Published && (article.ValidTo is null || article.ValidTo >= DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));

    private Task<bool> CanReadAsync(CancellationToken cancellationToken) => currentUser.HasPermissionAsync(Permissions.Knowledge.Read, cancellationToken);

    private async Task<bool> CanSeeUnpublishedAsync(CancellationToken cancellationToken) =>
        await currentUser.HasPermissionAsync(Permissions.Knowledge.Write, cancellationToken)
        || await currentUser.HasPermissionAsync(Permissions.Knowledge.Publish, cancellationToken);
}
