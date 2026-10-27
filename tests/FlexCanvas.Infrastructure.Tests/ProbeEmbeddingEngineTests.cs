using FlexCanvas.Application.Poc;
using FlexCanvas.Domain.Poc;
using FlexCanvas.Infrastructure.Win32;
using Xunit;

namespace FlexCanvas.Infrastructure.Tests;

public sealed class ProbeEmbeddingEngineTests
{
    [Fact]
    public void EmbeddingAndRepeatedDetachRestoreOriginalState()
    {
        var api = new FakeApi();
        var engine = new ProbeEmbeddingEngine(api);
        Assert.True(engine.Embed(10, 20).Success);
        Assert.Equal(EmbeddingState.Embedded, engine.Status!.State);
        Assert.Equal((nint)20, api.Parent);
        Assert.NotEqual(api.Snapshot.Style, api.Style);
        Assert.True(engine.Embed(10, 20).Success);
        Assert.True(engine.Detach().Success);
        Assert.Equal(api.Snapshot.Parent, api.Parent);
        Assert.Equal(api.Snapshot.Style, api.Style);
        Assert.Equal(api.Snapshot.Bounds, api.Bounds);
        int calls = api.Mutations;
        Assert.True(engine.Detach().Success);
        Assert.Equal(calls, api.Mutations);
        Assert.Equal(EmbeddingState.Detached, engine.Status.State);
    }

    [Fact]
    public void EmbeddingFailureRollsBackWithoutPretendingSuccess()
    {
        var api = new FakeApi { FailEmbeddingBounds = true };
        var engine = new ProbeEmbeddingEngine(api);
        var result = engine.Embed(10, 20);
        Assert.False(result.Success);
        Assert.Contains("Rollback: restored", result.Message);
        Assert.Equal(api.Snapshot.Parent, api.Parent);
        Assert.Equal(api.Snapshot.Style, api.Style);
        Assert.Equal(api.Snapshot.Bounds, api.Bounds);
        Assert.Equal(EmbeddingState.Failed, engine.Status!.State);
    }

    [Fact]
    public void AdapterExceptionAfterMutationStillRollsBack()
    {
        var api = new FakeApi { ThrowEmbeddingBounds = true };
        var engine = new ProbeEmbeddingEngine(api);
        var result = engine.Embed(10, 20);
        Assert.False(result.Success);
        Assert.Contains("InvalidOperationException", result.Message);
        Assert.Contains("Rollback: restored", result.Message);
        Assert.Equal(api.Snapshot.Parent, api.Parent);
        Assert.Equal(api.Snapshot.Style, api.Style);
        Assert.Equal(api.Snapshot.Bounds, api.Bounds);
    }

    [Fact]
    public void FailedRollbackRetainsSnapshotForExplicitRetry()
    {
        var api = new FakeApi { FailEmbeddingBounds = true, FailRestoreParent = true };
        var engine = new ProbeEmbeddingEngine(api);
        var result = engine.Embed(10, 20);
        Assert.False(result.Success);
        Assert.Equal(5, result.Win32Error);
        Assert.False(engine.Detach().Success);
        Assert.Equal(EmbeddingState.DetachFailed, engine.Status!.State);
        Assert.False(engine.Embed(11, 20).Success);
        api.FailRestoreParent = false;
        Assert.True(engine.Detach().Success);
        Assert.Equal(api.Snapshot.Style, api.Style);
        Assert.Equal(api.Snapshot.Bounds, api.Bounds);
    }

    [Fact]
    public void TargetExitDoesNotMutateAReusedHandle()
    {
        var api = new FakeApi();
        var engine = new ProbeEmbeddingEngine(api);
        Assert.True(engine.Embed(10, 20).Success);
        int mutations = api.Mutations;
        api.Alive = false;
        Assert.Equal(EmbeddingState.TargetExited, engine.Poll()!.State);
        Assert.True(engine.Detach().Success);
        Assert.Equal(mutations, api.Mutations);
    }

    [Fact]
    public void InvalidCaptureDoesNotExposeAStaleSession()
    {
        var api = new FakeApi();
        var engine = new ProbeEmbeddingEngine(api);
        Assert.True(engine.Embed(10, 20).Success);
        Assert.True(engine.Detach().Success);
        Assert.False(engine.Embed(0, 20).Success);
        Assert.Null(engine.Status);
    }

    [Fact]
    public void ResizeSynchronizesCurrentHostClientBounds()
    {
        var api = new FakeApi();
        var engine = new ProbeEmbeddingEngine(api);
        Assert.True(engine.Embed(10, 20).Success);
        api.Client = new(0, 0, 900, 650);
        Assert.True(engine.Resize().Success);
        Assert.Equal(api.Client, api.Bounds);
    }

    private sealed class FakeApi : IProbeWindowApi
    {
        internal ProbeSnapshot Snapshot { get; } = new(10, 42, 0, 0x10CF0000, 0, new(100, 100, 640, 480), 96);
        internal nint Parent;
        internal long Style = 0x10CF0000;
        internal ProbeBounds Bounds = new(100, 100, 640, 480);
        internal ProbeBounds Client = new(0, 0, 800, 600);
        internal bool Alive = true, FailEmbeddingBounds, FailRestoreParent, ThrowEmbeddingBounds;
        internal int Mutations;
        public ProbeResult Capture(nint target, nint host, out ProbeSnapshot? snapshot)
        {
            snapshot = target == 10 && host == 20 ? Snapshot : null;
            return snapshot is null ? ProbeResult.Fail("WINDOW_NOT_FOUND", "Invalid test target.") : ProbeResult.Ok();
        }
        public bool IsAlive(nint hwnd, uint processId) => Alive && hwnd == 10 && processId == 42;
        public ProbeResult SetStyles(nint hwnd, long style, long extendedStyle)
        {
            Mutations++;
            Style = style;
            return ProbeResult.Ok();
        }
        public ProbeResult SetParent(nint hwnd, nint parent)
        {
            Mutations++;
            if (parent == 0 && FailRestoreParent) return ProbeResult.Fail("DETACH_FAILED", "Restore parent failed.", 5);
            Parent = parent;
            return ProbeResult.Ok();
        }
        public ProbeResult SetBounds(nint hwnd, ProbeBounds bounds)
        {
            Mutations++;
            if (Parent == 20 && ThrowEmbeddingBounds) throw new InvalidOperationException("Injected adapter failure.");
            if (Parent == 20 && FailEmbeddingBounds) return ProbeResult.Fail("EMBEDDING_FAILED", "Size failed.");
            Bounds = bounds;
            return ProbeResult.Ok();
        }
        public ProbeResult GetClientBounds(nint hwnd, out ProbeBounds bounds)
        {
            bounds = Client;
            return ProbeResult.Ok();
        }
    }
}
