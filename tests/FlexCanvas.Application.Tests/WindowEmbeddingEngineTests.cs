using FlexCanvas.Domain;
using Xunit;

namespace FlexCanvas.Application.Tests;

public sealed class WindowEmbeddingEngineTests
{
    [Fact]
    public void SuccessAndIdempotentDetachRestoreSnapshot()
    {
        var api = new FakeApi();
        var engine = Engine(api);
        var result = engine.Embed(new(100, 42), 500);
        Assert.True(result.Success);
        Assert.Equal(WindowEmbeddingState.Embedded, result.Session!.State);
        Assert.True(result.Session.NeedsRestore);
        Assert.Equal((nint)500, api.Parent);
        Assert.True(engine.Detach(result.SessionId!.Value).Success);
        Assert.Equal((nint)0, api.Parent);
        Assert.Equal(api.Original.Style, api.Style);
        Assert.Equal(api.Original.Bounds, api.Bounds);
        int mutations = api.Mutations;
        Assert.True(engine.Detach(result.SessionId.Value).Success);
        Assert.Equal(mutations, api.Mutations);
    }
    [Fact]
    public void InvalidCaptureNeverCreatesARecoverySession()
    {
        var api = new FakeApi { RejectCapture = true };
        var result = Engine(api).Embed(new(100, 42), 500);
        Assert.False(result.Success);
        Assert.Null(result.Session);
        Assert.Equal(0, api.Mutations);
    }
    [Fact]
    public void RepeatedEmbedCannotOverwriteOriginalSnapshot()
    {
        var api = new FakeApi();
        var engine = Engine(api);
        var first = engine.Embed(new(100, 42), 500);
        var second = engine.Embed(new(100, 42), 501);
        Assert.False(second.Success);
        Assert.Equal(first.Session!.Original, second.Session!.Original);
        Assert.True(engine.Detach(first.SessionId!.Value).Success);
        Assert.Equal(api.Original.Style, api.Style);
    }
    [Fact]
    public void EmbedFailureAndRecoveryHaveSeparateErrorResults()
    {
        var api = new FakeApi { FailBounds = true };
        var result = Engine(api).Embed(new(100, 42), 500);
        Assert.False(result.Success);
        Assert.Equal(87, result.Win32ErrorCode);
        Assert.True(result.RecoveryResult!.Success);
        Assert.False(result.Session!.NeedsRestore);
        Assert.Equal(WindowEmbeddingState.Failed, result.Session.State);
        Assert.Equal(api.Original.Bounds, api.Bounds);
    }
    [Fact]
    public void FailedRollbackRetainsSnapshotAndCanRetryDetach()
    {
        var api = new FakeApi { FailBounds = true, FailRestoreParent = true };
        var engine = Engine(api);
        var result = engine.Embed(new(100, 42), 500);
        Assert.False(result.Success);
        Assert.Equal(87, result.Win32ErrorCode);
        Assert.False(result.RecoveryResult!.Success);
        Assert.Equal(5, result.RecoveryResult.Error);
        Assert.True(result.Session!.NeedsRestore);
        api.FailRestoreParent = false;
        Assert.True(engine.Detach(result.SessionId!.Value).Success);
        Assert.Equal(api.Original.Bounds, api.Bounds);
    }
    [Fact]
    public void AdapterExceptionAfterMutationStillRollsBack()
    {
        var api = new FakeApi { ThrowBoundsOnce = true };
        var result = Engine(api).Embed(new(100, 42), 500);
        Assert.False(result.Success);
        Assert.True(result.RecoveryResult!.Success);
        Assert.Equal(api.Original.Style, api.Style);
        Assert.Equal(api.Original.Bounds, api.Bounds);
    }
    [Fact]
    public void UnreadableIdentityDoesNotDiscardRecoveryState()
    {
        var api = new FakeApi();
        var engine = Engine(api);
        var result = engine.Embed(new(100, 42), 500);
        api.Unreadable = true;
        var failed = engine.Detach(result.SessionId!.Value);
        Assert.False(failed.Success);
        Assert.Equal(WindowEmbeddingState.DetachFailed, failed.Session!.State);
        Assert.True(failed.Session.NeedsRestore);
        api.Unreadable = false;
        Assert.True(engine.Detach(result.SessionId.Value).Success);
    }
    [Fact]
    public void ProcessGenerationChangeNeverMutatesReusedWindow()
    {
        var api = new FakeApi();
        var engine = Engine(api);
        var result = engine.Embed(new(100, 42), 500);
        api.Alive = false;
        int before = api.Mutations;
        var exited = engine.HandleEvent(new(result.SessionId!.Value, new(100, 42), WindowLifecycleEventKind.Destroyed));
        Assert.Equal(WindowEmbeddingState.TargetExited, exited.Session!.State);
        Assert.True(engine.Detach(result.SessionId.Value).Success);
        Assert.Equal(before, api.Mutations);
    }
    [Fact]
    public void DestroyNotificationAloneIsNotProofOfExit()
    {
        var api = new FakeApi();
        var engine = Engine(api);
        var result = engine.Embed(new(100, 42), 500);
        var status = engine.HandleEvent(new(result.SessionId!.Value, new(100, 42), WindowLifecycleEventKind.Destroyed));
        Assert.Equal(WindowEmbeddingState.Embedded, status.Session!.State);
        Assert.True(status.Session.NeedsRestore);
    }
    [Fact]
    public void OldSessionAndWrongPidEventsAreIgnored()
    {
        var api = new FakeApi();
        var engine = Engine(api);
        var result = engine.Embed(new(100, 42), 500);
        int before = api.Mutations;
        Assert.False(engine.HandleEvent(new(Guid.NewGuid(), new(100, 42), WindowLifecycleEventKind.LocationChanged)).Success);
        Assert.False(engine.HandleEvent(new(result.SessionId!.Value, new(100, 43), WindowLifecycleEventKind.LocationChanged)).Success);
        Assert.Equal(before, api.Mutations);
    }
    [Fact]
    public void ExplicitlyPlacedSessionsReadBackExternalSizesInsteadOfRefilling()
    {
        var api = new FakeApi();
        var engine = Engine(api);
        var result = engine.Embed(new(100, 42), 500);
        var id = result.SessionId!.Value;
        Assert.True(engine.Place(id, new(-6, -30, 800, 600)).Success);
        api.ExternalReading = new(0, 0, 900, 700);
        var read = engine.HandleEvent(new(id, new(100, 42), WindowLifecycleEventKind.LocationChanged));
        Assert.True(read.Success);
        Assert.True(read.Session!.ExternalBounds is { Width: 900, Height: 700 });
        Assert.Equal(new(-6, -30, 800, 600), api.Bounds);  // no refill happened
        // Echoes of our own placement (within tolerance) do not overwrite the pending read-back.
        var echo = engine.HandleEvent(new(id, new(100, 42), WindowLifecycleEventKind.LocationChanged));
        Assert.True(echo.Success);
        Assert.True(echo.Session!.ExternalBounds is { Width: 900 });
        // A later explicit placement supersedes the pending external size.
        Assert.True(engine.Place(id, new(-6, -30, 820, 610)).Success);
        Assert.Null(engine.GetSessions().Single(item => item.SessionId == id).ExternalBounds);
    }

    [Fact]
    public void S2SessionsKeepTheFillOnLocationChangeSemantics()
    {
        var api = new FakeApi();
        var engine = Engine(api);
        var result = engine.Embed(new(100, 42), 500);
        var filled = engine.HandleEvent(new(result.SessionId!.Value, new(100, 42), WindowLifecycleEventKind.LocationChanged));
        Assert.True(filled.Success);
        Assert.Equal(new(0, 0, 800, 600), api.Bounds);  // host client size, unchanged S2 behaviour
        Assert.Null(filled.Session!.ExternalBounds);
    }

    [Fact]
    public void PlaceAppliesExplicitBoundsWithoutFillingTheHost()
    {
        var api = new FakeApi();
        var engine = Engine(api);
        var result = engine.Embed(new(100, 42), 500);
        var placed = engine.Place(result.SessionId!.Value, new(-50, -25, 800, 600));
        Assert.True(placed.Success);
        Assert.Equal(new WindowBounds(-50, -25, 800, 600), api.Bounds);
        Assert.Equal(WindowEmbeddingState.Embedded, placed.Session!.State);
        Assert.True(placed.Session.NeedsRestore);
    }

    [Fact]
    public void PlaceRejectsMissingOrExitedSessionsWithoutMutating()
    {
        var api = new FakeApi();
        var engine = Engine(api);
        var result = engine.Embed(new(100, 42), 500);
        Assert.False(engine.Place(Guid.NewGuid(), new(0, 0, 640, 480)).Success);
        api.Alive = false;
        int before = api.Mutations;
        Assert.False(engine.Place(result.SessionId!.Value, new(0, 0, 640, 480)).Success);
        Assert.Equal(before, api.Mutations);
    }

    [Fact]
    public void PlaceFailureKeepsTheSessionRecoverable()
    {
        var api = new FakeApi();
        var engine = Engine(api);
        var result = engine.Embed(new(100, 42), 500);
        api.ThrowBoundsOnce = true;
        var failed = engine.Place(result.SessionId!.Value, new(0, 0, 800, 600));
        Assert.False(failed.Success);
        Assert.Equal(WindowEmbeddingState.Embedded, failed.Session!.State);
        Assert.True(failed.Session.NeedsRestore);
        Assert.True(engine.Detach(result.SessionId!.Value).Success);
        Assert.Equal(api.Original.Bounds, api.Bounds);
    }

    [Fact]
    public void IllegalTransitionsAndTerminalStatesCannotBeOverwritten()
    {
        var state = new WindowEmbeddingStateMachine();
        Assert.False(state.TryTransition(WindowEmbeddingState.Embedded));
        Assert.Equal(WindowEmbeddingState.Discovered, state.State);
        Assert.True(state.TryTransition(WindowEmbeddingState.Validating));
        Assert.True(state.TryTransition(WindowEmbeddingState.Embedding));
        Assert.True(state.TryTransition(WindowEmbeddingState.TargetExited));
        Assert.False(state.TryTransition(WindowEmbeddingState.Detaching));
    }
    private static WindowEmbeddingEngine Engine(FakeApi api) => new(api, new Sink());
    private sealed class Sink : IDiagnosticSink
    {
        public bool IsHealthy => true;
        public void Record(DiagnosticEntry entry) { }
    }
    private sealed class FakeApi : IWindowEmbeddingApi
    {
        public WindowSnapshot Original { get; } = new(new(100, 42), 123, 0, 0x10C40000, 0,
            new(10, 20, 640, 480), new(0, 1, 0, 0, 0, 0, new(10, 20, 640, 480)), 120);
        public nint Parent;
        public long Style = 0x10C40000;
        public WindowBounds Bounds = new(10, 20, 640, 480);
        public int Mutations;
        public bool RejectCapture, FailBounds, FailRestoreParent, ThrowBoundsOnce, Unreadable;
        public bool Alive = true;
        public NativeWindowResult Capture(WindowIdentity target, nint host, out WindowSnapshot? snapshot)
        { snapshot = RejectCapture ? null : Original; return RejectCapture ? NativeWindowResult.Fail(OperationCodes.InvalidArgument) : NativeWindowResult.Ok(); }
        public bool IsAlive(WindowSnapshot snapshot)
        { if (Unreadable) throw new InvalidOperationException("Private test text"); return Alive; }
        public NativeWindowResult SetStyles(nint hwnd, long style, long extendedStyle)
        { Mutations++; Style = style; return NativeWindowResult.Ok(); }
        public NativeWindowResult SetParent(nint hwnd, nint parent)
        {
            if (parent == 0 && FailRestoreParent) return NativeWindowResult.Fail(OperationCodes.EmbeddingFailed, NativeApi.SetParent, 5);
            Mutations++; Parent = parent; return NativeWindowResult.Ok();
        }
        public NativeWindowResult GetClientBounds(nint host, out WindowBounds bounds)
        { bounds = new(0, 0, 800, 600); return NativeWindowResult.Ok(); }
        public NativeWindowResult SetBounds(nint hwnd, WindowBounds bounds)
        {
            if (ThrowBoundsOnce) { ThrowBoundsOnce = false; throw new InvalidOperationException("Private test text"); }
            if (Parent != 0 && FailBounds) return NativeWindowResult.Fail(OperationCodes.EmbeddingFailed, NativeApi.SetWindowPos, 87);
            Mutations++; Bounds = bounds; return NativeWindowResult.Ok();
        }
        public NativeWindowResult RestorePlacement(nint hwnd, WindowPlacement placement) => NativeWindowResult.Ok();
        public NativeWindowResult VerifyRestored(WindowSnapshot snapshot) => NativeWindowResult.Ok();
        public NativeWindowResult Focus(nint hwnd) => NativeWindowResult.Ok();
        public WindowBounds? ExternalReading;
        public NativeWindowResult GetTargetBounds(nint hwnd, nint host, out WindowBounds bounds)
        { bounds = ExternalReading ?? Bounds; return NativeWindowResult.Ok(); }
    }
}
