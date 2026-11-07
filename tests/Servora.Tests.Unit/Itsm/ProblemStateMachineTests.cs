using Servora.Domain.Itsm;

namespace Servora.Tests.Unit.Itsm;

/// <summary>Tests for <see cref="ProblemStateMachine"/>.</summary>
public sealed class ProblemStateMachineTests
{
    [Theory]
    [InlineData(ProblemState.New, ProblemState.Assess)]
    [InlineData(ProblemState.Assess, ProblemState.RootCauseAnalysis)]
    [InlineData(ProblemState.RootCauseAnalysis, ProblemState.FixInProgress)]
    [InlineData(ProblemState.RootCauseAnalysis, ProblemState.Resolved)]
    [InlineData(ProblemState.FixInProgress, ProblemState.Resolved)]
    [InlineData(ProblemState.Resolved, ProblemState.Closed)]
    [InlineData(ProblemState.Resolved, ProblemState.FixInProgress)]
    public void CanTransition_AllowsValidTransitions(ProblemState from, ProblemState to)
    {
        Assert.True(ProblemStateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData(ProblemState.New, ProblemState.Resolved)]
    [InlineData(ProblemState.Assess, ProblemState.FixInProgress)]
    [InlineData(ProblemState.Closed, ProblemState.New)]
    [InlineData(ProblemState.Resolved, ProblemState.Cancelled)]
    public void CanTransition_RejectsInvalidTransitions(ProblemState from, ProblemState to)
    {
        Assert.False(ProblemStateMachine.CanTransition(from, to));
    }

    [Fact]
    public void FixInProgress_RequiresRootCause()
    {
        var problem = new Problem { State = ProblemState.FixInProgress };
        Assert.NotNull(ProblemStateMachine.ValidateStateFields(problem));

        problem.RootCause = "Missing index.";
        Assert.Null(ProblemStateMachine.ValidateStateFields(problem));
    }

    [Fact]
    public void Resolved_RequiresCodeAndFixNotes()
    {
        var problem = new Problem { State = ProblemState.Resolved, RootCause = "x" };
        Assert.NotNull(ProblemStateMachine.ValidateStateFields(problem));

        problem.ResolutionCode = ProblemResolutionCode.FixApplied;
        Assert.NotNull(ProblemStateMachine.ValidateStateFields(problem));

        problem.FixNotes = "Hotfix deployed.";
        Assert.Null(ProblemStateMachine.ValidateStateFields(problem));
    }

    [Fact]
    public void KnownError_RequiresWorkaround()
    {
        var problem = new Problem { IsKnownError = true };
        Assert.Equal("A known error must document a workaround.", ProblemStateMachine.ValidateStateFields(problem));

        problem.Workaround = "Restart the client.";
        Assert.Null(ProblemStateMachine.ValidateStateFields(problem));
    }

    [Fact]
    public void MainPath_StartsNewEndsClosed()
    {
        Assert.Equal(ProblemState.New, ProblemStateMachine.MainPath[0]);
        Assert.Equal(ProblemState.Closed, ProblemStateMachine.MainPath[^1]);
    }
}
