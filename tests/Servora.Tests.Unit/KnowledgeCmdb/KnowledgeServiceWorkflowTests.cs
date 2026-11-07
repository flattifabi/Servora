using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Application.Directory;
using Servora.Application.Knowledge;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Domain.Knowledge;

namespace Servora.Tests.Unit.KnowledgeCmdb;

/// <summary>End-to-end tests of the knowledge workflow, visibility rules, votes and search through the application services.</summary>
public sealed class KnowledgeServiceWorkflowTests : IDisposable
{
    private readonly KnowledgeCmdbTestDatabase _db = new();
    private readonly TestClock _clock = new();
    private readonly RecordingAuditService _audit = new();
    private readonly TestActor _author = new("Alice Author", Permissions.Knowledge.Read, Permissions.Knowledge.Write);
    private readonly TestActor _reviewer = new("Rita Reviewer", Permissions.Knowledge.Read, Permissions.Knowledge.Publish);
    private readonly TestActor _reader = new("Ron Reader", Permissions.Knowledge.Read);
    private readonly TestActor _manager = new("Mia Manager", Permissions.Knowledge.Read, Permissions.Knowledge.Manage);
    private readonly TestActor _nobody = new("Nora Nobody");

    /// <inheritdoc />
    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Article_GoesThroughDraftReviewPublishedRetired()
    {
        var baseId = await CreateBaseAsync();
        var created = await Service(_author).CreateAsync(Input(baseId, "Connect to the VPN", "Install the **GlobalProtect** client."));
        Assert.True(created.Succeeded, created.Error);
        var id = created.Value;

        // Drafts are invisible to readers and search.
        Assert.Equal(ResultErrorKind.NotFound, (await Service(_reader).GetAsync(id)).ErrorKind);
        Assert.Empty((await Search(_reader).SearchAsync(new KnowledgeSearchRequest("vpn"))).Value!);

        Assert.True((await Service(_author).TransitionAsync(id, KnowledgeWorkflowAction.SubmitForReview)).Succeeded);

        // Authors cannot publish their own work; reviewers can.
        Assert.Equal(ResultErrorKind.Forbidden, (await Service(_author).TransitionAsync(id, KnowledgeWorkflowAction.Publish)).ErrorKind);
        var published = await Service(_reviewer).TransitionAsync(id, KnowledgeWorkflowAction.Publish, "Looks good");
        Assert.True(published.Succeeded, published.Error);
        Assert.Contains(_audit.Entries, e => e.Action == "article.publish");

        var reading = await Service(_reader).GetAsync(id);
        Assert.True(reading.Succeeded);
        Assert.Equal(KnowledgeArticleState.Published, reading.Value!.State);
        Assert.Contains("<strong>GlobalProtect</strong>", reading.Value.Html, StringComparison.Ordinal);
        Assert.False(reading.Value.CanEdit);
        Assert.Empty(reading.Value.AllowedActions);
        var hits = (await Search(_reader).SearchAsync(new KnowledgeSearchRequest("vpn client"))).Value!;
        Assert.Single(hits);
        Assert.Equal(id, hits[0].Id);

        // Published content is locked until a new revision is started.
        Assert.Equal(ResultErrorKind.Validation, (await Service(_author).UpdateAsync(id, Input(baseId, "Changed", "x"))).ErrorKind);

        Assert.True((await Service(_reviewer).TransitionAsync(id, KnowledgeWorkflowAction.Retire)).Succeeded);
        Assert.Equal(ResultErrorKind.NotFound, (await Service(_reader).GetAsync(id)).ErrorKind);
        Assert.Empty((await Search(_reader).SearchAsync(new KnowledgeSearchRequest("vpn"))).Value!);
    }

    [Fact]
    public async Task InvalidTransitions_AreRejected()
    {
        var baseId = await CreateBaseAsync();
        var id = (await Service(_author).CreateAsync(Input(baseId, "Title", "Body"))).Value;

        Assert.Equal(ResultErrorKind.Validation, (await Service(_reviewer).TransitionAsync(id, KnowledgeWorkflowAction.Retire)).ErrorKind);
        Assert.True((await Service(_author).TransitionAsync(id, KnowledgeWorkflowAction.SubmitForReview)).Succeeded);
        Assert.Equal(ResultErrorKind.Validation, (await Service(_reviewer).TransitionAsync(id, KnowledgeWorkflowAction.Reject)).ErrorKind);
        Assert.True((await Service(_reviewer).TransitionAsync(id, KnowledgeWorkflowAction.Reject, "Add screenshots")).Succeeded);
        Assert.Equal(KnowledgeArticleState.Draft, (await Service(_author).GetAsync(id)).Value!.State);
    }

    [Fact]
    public async Task ContentChanges_CreateVersions()
    {
        var baseId = await CreateBaseAsync();
        var id = (await Service(_author).CreateAsync(Input(baseId, "Outlook", "v1"))).Value;
        var version = (await Service(_author).GetAsync(id)).Value!.Version;

        var input = Input(baseId, "Outlook", "v2 body");
        input.Version = version;
        input.ChangeNote = "Second draft";
        Assert.True((await Service(_author).UpdateAsync(id, input)).Succeeded);

        var versions = (await Service(_author).GetVersionsAsync(id)).Value!;
        Assert.Equal([2, 1], versions.Select(v => v.VersionNumber));
        Assert.Equal("Second draft", versions[0].ChangeNote);
        Assert.Equal("v1", (await Service(_author).GetVersionBodyAsync(id, versions[1].Id)).Value);

        // A stale version is a conflict.
        Assert.Equal(ResultErrorKind.Conflict, (await Service(_author).UpdateAsync(id, input)).ErrorKind);

        // Readers cannot see version history.
        Assert.Equal(ResultErrorKind.Forbidden, (await Service(_reader).GetVersionsAsync(id)).ErrorKind);
    }

    [Fact]
    public async Task Votes_AndViews_UpdateCounters()
    {
        var baseId = await CreateBaseAsync();
        var id = (await Service(_author).CreateAsync(Input(baseId, "MFA", "Register the app"))).Value;
        Assert.Equal(ResultErrorKind.NotFound, (await Service(_reader).VoteAsync(id, true)).ErrorKind);
        await Service(_reviewer).TransitionAsync(id, KnowledgeWorkflowAction.Publish);

        Assert.True((await Service(_reader).VoteAsync(id, true)).Succeeded);
        Assert.True((await Service(_reader).VoteAsync(id, false)).Succeeded);
        Assert.True((await Service(_author).VoteAsync(id, true)).Succeeded);
        Assert.True((await Service(_reader).RecordViewAsync(id)).Succeeded);
        Assert.True((await Service(_reader).RecordViewAsync(id)).Succeeded);

        var article = (await Service(_reader).GetAsync(id)).Value!;
        Assert.Equal(1, article.HelpfulCount);
        Assert.Equal(1, article.NotHelpfulCount);
        Assert.Equal(2, article.ViewCount);
        Assert.False(article.MyVote);
    }

    [Fact]
    public async Task ExpiredArticles_AreHiddenFromReaders()
    {
        var baseId = await CreateBaseAsync();
        var input = Input(baseId, "Old policy", "Body");
        input.ValidTo = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime).AddDays(-1);
        var id = (await Service(_author).CreateAsync(input)).Value;
        await Service(_reviewer).TransitionAsync(id, KnowledgeWorkflowAction.Publish);

        Assert.Equal(ResultErrorKind.NotFound, (await Service(_reader).GetAsync(id)).ErrorKind);
        Assert.True((await Service(_author).GetAsync(id)).Succeeded);
    }

    [Fact]
    public async Task Permissions_AreEnforced()
    {
        var baseId = await CreateBaseAsync();

        Assert.Equal(ResultErrorKind.Forbidden, (await Service(_nobody).ListAsync(new KnowledgeArticleQuery())).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await Service(_reader).CreateAsync(Input(baseId, "x", "y"))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await Service(_author).SaveBaseAsync(null, new KnowledgeBaseInput { Name = "Other" })).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await Search(_nobody).SearchAsync(new KnowledgeSearchRequest("x"))).ErrorKind);
        var id = (await Service(_author).CreateAsync(Input(baseId, "x", "y"))).Value;
        Assert.Equal(ResultErrorKind.Forbidden, (await Service(_author).DeleteAsync(id)).ErrorKind);
        Assert.True((await Service(_manager).DeleteAsync(id)).Succeeded);
    }

    [Fact]
    public async Task Create_ValidatesInput()
    {
        var baseId = await CreateBaseAsync();

        Assert.Equal(ResultErrorKind.Validation, (await Service(_author).CreateAsync(Input(baseId, "  ", "Body"))).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await Service(_author).CreateAsync(Input(Guid.NewGuid(), "Title", "Body"))).ErrorKind);
        var input = Input(baseId, "Title", "Body");
        input.CategoryId = Guid.NewGuid();
        Assert.Equal(ResultErrorKind.Validation, (await Service(_author).CreateAsync(input)).ErrorKind);
    }

    private static KnowledgeArticleInput Input(Guid baseId, string title, string body) => new() { KnowledgeBaseId = baseId, Title = title, Body = body, Summary = "Summary of " + title };

    private async Task<Guid> CreateBaseAsync()
    {
        var result = await Service(_manager).SaveBaseAsync(null, new KnowledgeBaseInput { Name = "IT " + Guid.NewGuid().ToString("N")[..6] });
        Assert.True(result.Succeeded, result.Error);
        return result.Value;
    }

    private KnowledgeService Service(ICurrentUser actor) => new(
        _db,
        actor,
        new NumberSequenceService(_db),
        new ActivityService(_db, actor, _clock),
        _audit,
        new DirectoryLookupService(_db),
        _clock);

    private KnowledgeSearchService Search(ICurrentUser actor) => new(_db, actor, _clock);
}
