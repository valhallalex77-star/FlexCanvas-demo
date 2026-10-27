using FlexCanvas.Domain.Poc;
using Xunit;

namespace FlexCanvas.Domain.Tests;

public sealed class EmbeddingStateMachineTests
{
    [Fact]
    public void SuccessfulLifecycleHasOnlyAllowedTransitions()
    {
        var state = new EmbeddingStateMachine();
        foreach (var next in new[] { EmbeddingState.Validating, EmbeddingState.Embedding, EmbeddingState.Embedded,
                     EmbeddingState.Detaching, EmbeddingState.Detached })
            Assert.True(state.TryTransition(next));
        Assert.Equal(EmbeddingState.Detached, state.State);
        Assert.False(state.TryTransition(EmbeddingState.Embedded));
    }

    [Fact]
    public void FailedRollbackCanRetryDetach()
    {
        var state = new EmbeddingStateMachine();
        foreach (var next in new[] { EmbeddingState.Validating, EmbeddingState.Embedding, EmbeddingState.RollingBack,
                     EmbeddingState.Failed, EmbeddingState.Detaching, EmbeddingState.DetachFailed,
                     EmbeddingState.Detaching, EmbeddingState.Detached })
            Assert.True(state.TryTransition(next));
    }

    [Theory]
    [InlineData(EmbeddingState.Embedding)]
    [InlineData(EmbeddingState.Embedded)]
    [InlineData(EmbeddingState.Detaching)]
    [InlineData(EmbeddingState.Detached)]
    [InlineData(EmbeddingState.TargetExited)]
    public void InvalidInitialTransitionDoesNotMutateState(EmbeddingState attempted)
    {
        var state = new EmbeddingStateMachine();
        Assert.False(state.TryTransition(attempted));
        Assert.Equal(EmbeddingState.Discovered, state.State);
    }

    [Fact]
    public void TargetExitFromEmbeddedIsTerminal()
    {
        var state = new EmbeddingStateMachine();
        Assert.True(state.TryTransition(EmbeddingState.Validating));
        Assert.True(state.TryTransition(EmbeddingState.Embedding));
        Assert.True(state.TryTransition(EmbeddingState.Embedded));
        Assert.True(state.TryTransition(EmbeddingState.TargetExited));
        Assert.False(state.TryTransition(EmbeddingState.Detaching));
    }

    [Fact]
    public void ResultsDistinguishSuccessAndNativeFailure()
    {
        Assert.True(ProbeResult.Ok().Success);
        var result = ProbeResult.Fail("ACCESS_DENIED", "Blocked.", 5);
        Assert.False(result.Success);
        Assert.Equal("ACCESS_DENIED", result.Code);
        Assert.Equal(5, result.Win32Error);
    }
}
