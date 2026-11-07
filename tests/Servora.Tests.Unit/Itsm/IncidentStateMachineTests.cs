using Servora.Domain.Itsm;

namespace Servora.Tests.Unit.Itsm;

/// <summary>Tests for <see cref="IncidentStateMachine"/>.</summary>
public sealed class IncidentStateMachineTests
{
    [Theory]
    [InlineData(IncidentState.New, IncidentState.InProgress)]
    [InlineData(IncidentState.New, IncidentState.Resolved)]
    [InlineData(IncidentState.New, IncidentState.Cancelled)]
    [InlineData(IncidentState.InProgress, IncidentState.OnHold)]
    [InlineData(IncidentState.OnHold, IncidentState.InProgress)]
    [InlineData(IncidentState.InProgress, IncidentState.Resolved)]
    [InlineData(IncidentState.Resolved, IncidentState.Closed)]
    [InlineData(IncidentState.Resolved, IncidentState.InProgress)]
    [InlineData(IncidentState.Closed, IncidentState.Closed)]
    public void CanTransition_AllowsValidTransitions(IncidentState from, IncidentState to)
    {
        Assert.True(IncidentStateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData(IncidentState.New, IncidentState.Closed)]
    [InlineData(IncidentState.InProgress, IncidentState.New)]
    [InlineData(IncidentState.Resolved, IncidentState.Cancelled)]
    [InlineData(IncidentState.Closed, IncidentState.InProgress)]
    [InlineData(IncidentState.Cancelled, IncidentState.New)]
    [InlineData(IncidentState.Cancelled, IncidentState.Resolved)]
    public void CanTransition_RejectsInvalidTransitions(IncidentState from, IncidentState to)
    {
        Assert.False(IncidentStateMachine.CanTransition(from, to));
    }

    [Fact]
    public void FinalStates_HaveNoTargets()
    {
        Assert.Empty(IncidentStateMachine.AllowedTargets(IncidentState.Closed));
        Assert.Empty(IncidentStateMachine.AllowedTargets(IncidentState.Cancelled));
        Assert.True(IncidentStateMachine.IsFinal(IncidentState.Closed));
        Assert.False(IncidentStateMachine.IsActive(IncidentState.Cancelled));
        Assert.True(IncidentStateMachine.IsActive(IncidentState.Resolved));
        Assert.False(IncidentStateMachine.IsOpen(IncidentState.Resolved));
    }

    [Fact]
    public void OnHold_RequiresReason()
    {
        var incident = new Incident { State = IncidentState.OnHold };
        Assert.NotNull(IncidentStateMachine.ValidateStateFields(incident));

        incident.OnHoldReason = OnHoldReason.AwaitingCaller;
        Assert.Null(IncidentStateMachine.ValidateStateFields(incident));
    }

    [Fact]
    public void Resolved_RequiresCodeAndNotes()
    {
        var incident = new Incident { State = IncidentState.Resolved };
        Assert.Contains("resolution code", IncidentStateMachine.ValidateStateFields(incident));

        incident.ResolutionCode = IncidentResolutionCode.SolvedRemotely;
        Assert.Contains("notes", IncidentStateMachine.ValidateStateFields(incident));

        incident.ResolutionNotes = "   ";
        Assert.NotNull(IncidentStateMachine.ValidateStateFields(incident));

        incident.ResolutionNotes = "Restarted the service.";
        Assert.Null(IncidentStateMachine.ValidateStateFields(incident));
    }

    [Fact]
    public void ValidateTransition_ReportsInvalidMove()
    {
        var incident = new Incident { State = IncidentState.InProgress };
        var error = IncidentStateMachine.ValidateTransition(IncidentState.Closed, incident);
        Assert.Equal("An incident cannot move from Closed to In Progress.", error);
    }
}
