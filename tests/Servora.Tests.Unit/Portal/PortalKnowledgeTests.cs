using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Portal;
using Servora.Application.Security;
using Servora.Domain.Knowledge;

namespace Servora.Tests.Unit.Portal;

/// <summary>Portal knowledge: only published, valid articles of active knowledge bases are visible to portal users.</summary>
public sealed class PortalKnowledgeTests : IDisposable
{
    private readonly PortalTestHost _host = new();

    [Fact]
    public async Task PortalOnlyUser_SeesPublishedArticles_ButNeverDraftsReviewRetiredExpiredOrInactiveBases()
    {
        var kb = await _host.AddKnowledgeBaseAsync();
        var inactive = await _host.AddKnowledgeBaseAsync(active: false);
        var published = await _host.AddArticleAsync(kb, "Reset VPN password", KnowledgeArticleState.Published);
        var draft = await _host.AddArticleAsync(kb, "VPN draft notes", KnowledgeArticleState.Draft);
        var review = await _host.AddArticleAsync(kb, "VPN review notes", KnowledgeArticleState.Review);
        var retired = await _host.AddArticleAsync(kb, "Old VPN client", KnowledgeArticleState.Retired);
        var expired = await _host.AddArticleAsync(kb, "VPN winter guide", KnowledgeArticleState.Published, DateOnly.FromDateTime(_host.Clock.UtcNow.UtcDateTime.AddDays(-1)));
        var hidden = await _host.AddArticleAsync(inactive, "VPN in hidden base", KnowledgeArticleState.Published);

        var anna = await _host.AddUserAsync("Anna Employee");
        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var knowledge = _host.Get<IPortalKnowledgeService>();

        var hits = (await knowledge.SearchAsync("vpn")).Value!;
        Assert.Equal([published], hits.Select(h => h.Id).ToArray());
        Assert.False(string.IsNullOrEmpty(hits[0].Snippet));

        var article = await knowledge.GetArticleAsync(published);
        Assert.True(article.Succeeded, article.Error);
        Assert.Contains("<h2", article.Value!.SafeHtml, StringComparison.Ordinal);
        Assert.Equal(2, article.Value.Headings.Count);

        foreach (var invisible in new[] { draft, review, retired, expired, hidden })
        {
            Assert.Equal(ResultErrorKind.NotFound, (await knowledge.GetArticleAsync(invisible)).ErrorKind);
            Assert.Equal(ResultErrorKind.NotFound, (await knowledge.VoteAsync(invisible, true)).ErrorKind);
        }

        var home = (await knowledge.GetHomeAsync()).Value!;
        Assert.Equal(1, home.ArticleCount);
        Assert.Equal([published], home.Popular.Select(a => a.Id).ToArray());
        Assert.Equal(1, Assert.Single(home.Categories).ArticleCount);
        Assert.Single((await knowledge.ListByCategoryAsync(kb.CategoryId)).Value!);
    }

    [Fact]
    public async Task Votes_AreOnePerUser_AndViewsAreCounted()
    {
        var kb = await _host.AddKnowledgeBaseAsync();
        var published = await _host.AddArticleAsync(kb, "Set up MFA", KnowledgeArticleState.Published);
        var anna = await _host.AddUserAsync("Anna Employee");
        var bob = await _host.AddUserAsync("Bob Builder");

        _host.User.Become(anna, "Anna Employee", Permissions.Portal.Access);
        var knowledge = _host.Get<IPortalKnowledgeService>();
        Assert.True((await knowledge.VoteAsync(published, true)).Succeeded);
        Assert.True((await knowledge.VoteAsync(published, false)).Succeeded);
        Assert.True((await knowledge.RecordViewAsync(published)).Succeeded);
        Assert.False((await knowledge.GetArticleAsync(published)).Value!.MyVote);

        _host.User.Become(bob, "Bob Builder", Permissions.Portal.Access);
        Assert.True((await knowledge.VoteAsync(published, true)).Succeeded);

        await using var db = _host.Create();
        var article = await db.Set<KnowledgeArticle>().SingleAsync(a => a.Id == published);
        Assert.Equal(1, article.HelpfulCount);
        Assert.Equal(1, article.NotHelpfulCount);
        Assert.Equal(1, article.ViewCount);
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();
}
