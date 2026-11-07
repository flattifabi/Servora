using Microsoft.EntityFrameworkCore;
using Servora.Application.Events;
using Servora.Application.Itsm;
using Servora.Domain.Common;
using Servora.Domain.Itsm;

namespace Servora.Tests.Unit.Itsm;

/// <summary>Tests for <see cref="ItsmSavingHook"/> (directly and through <c>ServoraDbContext</c> on SQLite).</summary>
public sealed class ItsmSavingHookTests
{
    private readonly FixedClock _clock = new();

    [Fact]
    public async Task Insert_CalculatesPriorityFromMatrix()
    {
        var incident = new Incident { Impact = Impact.High, Urgency = Urgency.High, Priority = Priority.Planning };
        var context = new RecordSavingContext(TableNames.Incident, incident, RecordChangeType.Created, []);

        await new ItsmSavingHook(_clock).OnSavingAsync(context, CancellationToken.None);

        Assert.False(context.IsAborted);
        Assert.Equal(Priority.Critical, incident.Priority);
        Assert.True(incident.Active);
        Assert.Equal(_clock.UtcNow, incident.OpenedAt);
    }

    [Fact]
    public async Task Update_RecalculatesPriorityWhenUrgencyChanges()
    {
        var incident = new Incident { Impact = Impact.Medium, Urgency = Urgency.High, Priority = Priority.Low, State = IncidentState.InProgress, OpenedAt = _clock.UtcNow };
        var changes = new[] { new RecordFieldChange(nameof(Incident.Urgency), Urgency.Low, Urgency.High) };
        var context = new RecordSavingContext(TableNames.Incident, incident, RecordChangeType.Updated, changes);

        await new ItsmSavingHook(_clock).OnSavingAsync(context, CancellationToken.None);

        Assert.Equal(Priority.High, incident.Priority);
    }

    [Fact]
    public async Task Update_KeepsPriorityWhenImpactAndUrgencyUnchanged()
    {
        var incident = new Incident { Impact = Impact.Low, Urgency = Urgency.Low, Priority = Priority.Critical, State = IncidentState.InProgress, OpenedAt = _clock.UtcNow };
        var changes = new[] { new RecordFieldChange(nameof(Incident.ShortDescription), "a", "b") };
        var context = new RecordSavingContext(TableNames.Incident, incident, RecordChangeType.Updated, changes);

        await new ItsmSavingHook(_clock).OnSavingAsync(context, CancellationToken.None);

        Assert.Equal(Priority.Critical, incident.Priority);
    }

    [Fact]
    public async Task Update_AbortsInvalidTransition()
    {
        var incident = new Incident { State = IncidentState.InProgress, OpenedAt = _clock.UtcNow };
        var changes = new[] { new RecordFieldChange(nameof(Incident.State), IncidentState.Closed, IncidentState.InProgress) };
        var context = new RecordSavingContext(TableNames.Incident, incident, RecordChangeType.Updated, changes);

        await new ItsmSavingHook(_clock).OnSavingAsync(context, CancellationToken.None);

        Assert.True(context.IsAborted);
        Assert.Contains("cannot move from Closed", context.AbortMessage);
    }

    [Fact]
    public async Task Update_AbortsResolveWithoutNotes()
    {
        var incident = new Incident { State = IncidentState.Resolved, ResolutionCode = IncidentResolutionCode.SolvedRemotely, OpenedAt = _clock.UtcNow };
        var changes = new[] { new RecordFieldChange(nameof(Incident.State), IncidentState.InProgress, IncidentState.Resolved) };
        var context = new RecordSavingContext(TableNames.Incident, incident, RecordChangeType.Updated, changes);

        await new ItsmSavingHook(_clock).OnSavingAsync(context, CancellationToken.None);

        Assert.True(context.IsAborted);
    }

    [Fact]
    public async Task Close_MaintainsActiveAndTimestamps()
    {
        var incident = new Incident
        {
            State = IncidentState.Closed,
            ResolutionCode = IncidentResolutionCode.SolvedPermanently,
            ResolutionNotes = "Fixed.",
            OpenedAt = _clock.UtcNow.AddDays(-1),
        };
        var changes = new[] { new RecordFieldChange(nameof(Incident.State), IncidentState.Resolved, IncidentState.Closed) };
        var context = new RecordSavingContext(TableNames.Incident, incident, RecordChangeType.Updated, changes);

        await new ItsmSavingHook(_clock).OnSavingAsync(context, CancellationToken.None);

        Assert.False(context.IsAborted);
        Assert.False(incident.Active);
        Assert.Equal(_clock.UtcNow, incident.ClosedAt);
        Assert.Equal(_clock.UtcNow, incident.ResolvedAt);
    }

    [Fact]
    public async Task Problem_PriorityAndKnownErrorRule()
    {
        var problem = new Problem { Impact = Impact.Medium, Urgency = Urgency.Medium, IsKnownError = true };
        var context = new RecordSavingContext(TableNames.Problem, problem, RecordChangeType.Created, []);

        await new ItsmSavingHook(_clock).OnSavingAsync(context, CancellationToken.None);

        Assert.Equal(Priority.Moderate, problem.Priority);
        Assert.True(context.IsAborted);
    }

    [Fact]
    public async Task DbContext_AppliesHookOnSaveAndBlocksInvalidTransition()
    {
        await using var host = await ItsmTestHost.CreateAsync();
        var incident = new Incident { Number = "INC9000001", ShortDescription = "Printer offline", Impact = Impact.High, Urgency = Urgency.Medium };
        await using (var db = host.Create())
        {
            db.Set<Incident>().Add(incident);
            await db.SaveChangesAsync();
        }

        await using (var db = host.Create())
        {
            var stored = await db.Set<Incident>().SingleAsync(i => i.Id == incident.Id);
            Assert.Equal(Priority.High, stored.Priority);
            Assert.True(stored.Active);

            stored.State = IncidentState.Closed;
            var ex = await Assert.ThrowsAsync<RecordSaveAbortedException>(() => db.SaveChangesAsync());
            Assert.Contains("cannot move from New to Closed", ex.Message);
        }
    }
}
