using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Observability;
using Servora.Infrastructure.Observability;

namespace Servora.Tests.Unit.Observability;

/// <summary>Tests that the audit hash-chain verification detects tampering.</summary>
public sealed class AuditChainVerificationTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly TestCurrentUser _user = new(Permissions.Observability.ReadAudit, Permissions.Configuration.ManageSettings);

    [Fact]
    public async Task UntouchedChain_IsIntact()
    {
        await WriteEventsAsync(10);

        var result = await CreateService().VerifyChainAsync();

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.IsIntact);
        Assert.Equal(10, result.Value.CheckedCount);
        Assert.Equal(1, result.Value.FirstSequence);
        Assert.Equal(10, result.Value.LastSequence);
    }

    [Fact]
    public async Task ModifiedEvent_IsDetectedAsHashMismatch()
    {
        await WriteEventsAsync(6);
        await using (var db = _database.Create())
        {
            var victim = await db.Set<AuditEvent>().SingleAsync(a => a.Sequence == 3);
            victim.Message = "Nothing to see here";
            await db.SaveChangesAsync();
        }

        var result = (await CreateService().VerifyChainAsync()).Value!;

        Assert.False(result.IsIntact);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(AuditChainIssueKind.HashMismatch, issue.Kind);
        Assert.Equal(3, issue.Sequence);
    }

    [Fact]
    public async Task ModifiedEventWithRecomputedHash_BreaksTheNextLink()
    {
        await WriteEventsAsync(5);
        await using (var db = _database.Create())
        {
            var victim = await db.Set<AuditEvent>().SingleAsync(a => a.Sequence == 2);
            victim.Outcome = AuditOutcome.Success;
            victim.ActorName = "someone else";
            victim.Hash = AuditService.ComputeHash(victim);
            await db.SaveChangesAsync();
        }

        var result = (await CreateService().VerifyChainAsync()).Value!;

        Assert.False(result.IsIntact);
        Assert.Contains(result.Issues, i => i.Kind == AuditChainIssueKind.BrokenLink && i.Sequence == 3);
    }

    [Fact]
    public async Task DeletedEvent_IsDetected()
    {
        await WriteEventsAsync(5);
        await using (var db = _database.Create())
        {
            await db.Set<AuditEvent>().Where(a => a.Sequence == 4).ExecuteDeleteAsync();
        }

        var result = (await CreateService().VerifyChainAsync()).Value!;

        Assert.False(result.IsIntact);
        Assert.Contains(result.Issues, i => i.Kind == AuditChainIssueKind.BrokenLink && i.Sequence == 5);
    }

    [Fact]
    public async Task DeletedOldestEvent_IsDetectedAsMissingStart()
    {
        await WriteEventsAsync(3);
        await using (var db = _database.Create())
        {
            await db.Set<AuditEvent>().Where(a => a.Sequence == 1).ExecuteDeleteAsync();
        }

        var result = (await CreateService().VerifyChainAsync()).Value!;

        Assert.Contains(result.Issues, i => i.Kind == AuditChainIssueKind.MissingStart);
    }

    [Fact]
    public async Task RetentionPruning_KeepsChainVerifiable()
    {
        await WriteEventsAsync(4);
        _clock.Advance(TimeSpan.FromDays(100));
        await WriteEventsAsync(3);
        var service = CreateService();
        Assert.True((await service.SetRetentionDaysAsync(30)).Succeeded);

        var deleted = await service.PruneAsync();
        var result = (await service.VerifyChainAsync()).Value!;

        Assert.Equal(4, deleted);
        Assert.True(result.IsIntact, string.Join("; ", result.Issues.Select(i => i.Message)));
        Assert.Equal(5, result.FirstSequence);
    }

    [Fact]
    public async Task Verification_RequiresAuditPermission()
    {
        var service = new AuditLogService(new TestCurrentUser(), _database, CreateWriter(), _clock, NullLogger<AuditLogService>.Instance);

        Assert.Equal(Application.Common.ResultErrorKind.Forbidden, (await service.VerifyChainAsync()).ErrorKind);
    }

    [Fact]
    public void CsvEscape_PreventsFormulaInjectionAndQuotes()
    {
        Assert.Equal("'=HYPERLINK(1)", AuditCsvWriter.Escape("=HYPERLINK(1)"));
        Assert.Equal("\"a,\"\"b\"\"\"", AuditCsvWriter.Escape("a,\"b\""));
        Assert.Equal("plain", AuditCsvWriter.Escape("plain"));
    }

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    private AuditService CreateWriter() => new(_database, _user, _clock, NullLogger<AuditService>.Instance);

    private AuditLogService CreateService() => new(_user, _database, CreateWriter(), _clock, NullLogger<AuditLogService>.Instance);

    private async Task WriteEventsAsync(int count)
    {
        var writer = CreateWriter();
        for (var i = 0; i < count; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            await writer.WriteAsync(new AuditEntry("test", "event." + i.ToString(CultureInfo.InvariantCulture), i % 3 == 0 ? AuditOutcome.Failure : AuditOutcome.Success)
            {
                Message = $"Event {i}",
                Details = new { index = i },
            });
        }
    }
}
