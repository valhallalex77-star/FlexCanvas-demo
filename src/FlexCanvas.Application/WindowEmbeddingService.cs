using FlexCanvas.Domain;

namespace FlexCanvas.Application;

/// <summary>Native port invoked exclusively by the serialized window dispatcher.</summary>
public interface IWindowEmbeddingApi
{
    NativeWindowResult Capture(WindowIdentity target, nint host, out WindowSnapshot? snapshot);
    bool IsAlive(WindowSnapshot snapshot);
    NativeWindowResult SetStyles(nint hwnd, long style, long extendedStyle);
    NativeWindowResult SetParent(nint hwnd, nint parent);
    NativeWindowResult GetClientBounds(nint host, out WindowBounds bounds);
    NativeWindowResult SetBounds(nint hwnd, WindowBounds bounds);
    NativeWindowResult RestorePlacement(nint hwnd, WindowPlacement placement);
    NativeWindowResult VerifyRestored(WindowSnapshot snapshot);
    NativeWindowResult Focus(nint hwnd);
    /// <summary>Reads the target's current bounds relative to its host client area; used for external size read-back.</summary>
    NativeWindowResult GetTargetBounds(nint hwnd, nint host, out WindowBounds bounds);
}

/// <summary>Production orchestration, independent of WPF and prototype contracts.</summary>
public interface IWindowEmbeddingService
{
    Task<EmbeddingResult> EmbedAsync(WindowIdentity target, nint host, CancellationToken cancellationToken = default);
    Task<EmbeddingResult> ResizeAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<EmbeddingResult> PlaceAsync(Guid sessionId, WindowBounds explicitBounds, CancellationToken cancellationToken = default);
    Task<EmbeddingResult> FocusAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<EmbeddingResult> DetachAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmbeddingSessionStatus>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<EmbeddingResult> HandleEventAsync(WindowLifecycleEvent notification, CancellationToken cancellationToken = default);
}

/// <summary>Multi-session recovery engine; callers must serialize every entry point.</summary>
public sealed class WindowEmbeddingEngine(IWindowEmbeddingApi api, IDiagnosticSink diagnostics)
{
    private sealed class Session(Guid id, nint host, WindowSnapshot snapshot)
    {
        internal readonly Guid Id = id;
        internal readonly nint Host = host;
        internal readonly WindowSnapshot Snapshot = snapshot;
        internal readonly WindowEmbeddingStateMachine State = new();
        internal bool NeedsRestore;
        internal NativeWindowResult Last = NativeWindowResult.Ok();
        internal NativeWindowResult? Recovery;
        // Explicitly placed (canvas) sessions track their native bounds to distinguish external changes from echoes.
        internal bool ExplicitlyPlaced;
        internal WindowBounds? LastNative;
        internal WindowBounds? External;
        internal EmbeddingSessionStatus View => new(Id, Host, Snapshot, State.State, NeedsRestore, Last, Recovery, External);
    }
    private readonly Dictionary<Guid, Session> _sessions = [];

    public IReadOnlyList<EmbeddingSessionStatus> GetSessions()
    {
        foreach (var session in _sessions.Values) CheckAlive(session);
        return _sessions.Values.Select(s => s.View).ToArray();
    }

    public EmbeddingResult Embed(WindowIdentity target, nint host)
    {
        foreach (var existing in _sessions.Values)
        {
            CheckAlive(existing);
            if (!existing.NeedsRestore) continue;
            if (existing.Snapshot.Identity == target || existing.Host == host)
                return Result(existing, NativeWindowResult.Fail(OperationCodes.InvalidArgument), "Target or host already has an active session.");
        }
        Guid id = Guid.NewGuid();
        WindowSnapshot? snapshot = null;
        NativeWindowResult capture;
        try { capture = api.Capture(target, host, out snapshot); }
        catch (Exception error) { return Failure(id, error); }
        if (!capture.Success || snapshot is null)
            return new(false, capture.Success ? OperationCodes.InternalError : capture.Code, "Target validation failed; no window was modified.", id, null, capture.Api, capture.Error);
        // Terminal sessions have no native resources and need not grow indefinitely.
        foreach (var key in _sessions.Where(p => !p.Value.NeedsRestore).Select(p => p.Key).ToArray()) _sessions.Remove(key);
        var session = new Session(id, host, snapshot);
        session.State.TryTransition(WindowEmbeddingState.Validating);
        session.State.TryTransition(WindowEmbeddingState.Embedding);
        session.NeedsRestore = true;
        _sessions.Add(id, session);
        long childStyle = (snapshot.Style & ~(0x80000000L | 0x00C00000L | 0x00040000L | 0x00030000L)) | 0x40000000L;
        var result = Invoke(session, () => api.SetStyles(target.Hwnd, childStyle, snapshot.ExtendedStyle));
        if (result.Success) result = Invoke(session, () => api.SetParent(target.Hwnd, host));
        if (result.Success) result = ResizeNative(session);
        if (result.Success)
        {
            session.State.TryTransition(WindowEmbeddingState.Embedded);
            Log(session, DiagnosticEvent.Embedding, result);
            return Result(session, result, "Embedded; input and third-party compatibility still require verification.");
        }
        session.Last = result;
        if (!CheckAlive(session)) return Result(session, result, "Target exited during embedding.");
        session.State.TryTransition(WindowEmbeddingState.RollingBack);
        var recovery = Restore(session);
        session.Recovery = recovery;
        session.NeedsRestore = !recovery.Success;
        if (session.State.State != WindowEmbeddingState.TargetExited) session.State.TryTransition(WindowEmbeddingState.Failed);
        Log(session, DiagnosticEvent.Rollback, recovery);
        return Result(session, result, recovery.Success ? "Embedding failed; original window restored." : "Embedding failed; recovery failed. Keep host open and retry Detach.");
    }

    public EmbeddingResult Resize(Guid id)
    {
        if (!_sessions.TryGetValue(id, out var session)) return Missing(id);
        if (!CheckAlive(session) || session.State.State != WindowEmbeddingState.Embedded) return Missing(id, session.View);
        var result = ResizeNative(session);
        if (!result.Success) Log(session, DiagnosticEvent.NativeRead, result);
        return Result(session, result, result.Success ? "Client size synchronized." : "Size synchronization failed.");
    }

    /// <summary>Applies explicit native bounds (e.g. 1:1 canvas placement with a clipping host); never fills the host.</summary>
    public EmbeddingResult Place(Guid id, WindowBounds bounds)
    {
        if (!_sessions.TryGetValue(id, out var session)) return Missing(id);
        if (!CheckAlive(session) || session.State.State != WindowEmbeddingState.Embedded) return Missing(id, session.View);
        var result = Invoke(session, () => api.SetBounds(session.Snapshot.Identity.Hwnd, bounds));
        if (result.Success)
        {
            session.ExplicitlyPlaced = true;
            session.LastNative = bounds;
            session.External = null;  // a successful explicit placement supersedes a pending external read-back
        }
        if (!result.Success) Log(session, DiagnosticEvent.NativeRead, result);
        return Result(session, result, result.Success
            ? "Explicit native placement applied."
            : "Explicit placement failed; keep the host open and retry.");
    }

    public EmbeddingResult Focus(Guid id)
    {
        if (!_sessions.TryGetValue(id, out var session)) return Missing(id);
        if (!CheckAlive(session) || session.State.State != WindowEmbeddingState.Embedded) return Missing(id, session.View);
        var result = Invoke(session, () => api.Focus(session.Snapshot.Identity.Hwnd));
        return Result(session, result, result.Success ? "Focus requested by user." : "Focus request rejected; click inside the hosted application.");
    }

    public EmbeddingResult Detach(Guid id)
    {
        if (!_sessions.TryGetValue(id, out var session)) return new(true, OperationCodes.Ok, "No active session.", id, null);
        CheckAlive(session);
        if (!session.NeedsRestore) return Result(session, NativeWindowResult.Ok(), "Already detached or target exited.");
        if (!session.State.TryTransition(WindowEmbeddingState.Detaching))
            return Result(session, NativeWindowResult.Fail(OperationCodes.InvalidArgument), "Invalid detach state.");
        var result = Restore(session);
        session.Recovery = result;
        session.NeedsRestore = !result.Success;
        if (session.State.State != WindowEmbeddingState.TargetExited)
            session.State.TryTransition(result.Success ? WindowEmbeddingState.Detached : WindowEmbeddingState.DetachFailed);
        Log(session, DiagnosticEvent.Detaching, result);
        return Result(session, result.Success ? result : result with { Code = OperationCodes.DetachFailed },
            result.Success ? "Original window restored." : "Restore failed; snapshot and host must be retained for retry.");
    }

    public EmbeddingResult HandleEvent(WindowLifecycleEvent notification)
    {
        if (!_sessions.TryGetValue(notification.SessionId, out var session)
            || session.Snapshot.Identity != notification.Identity) return Missing(notification.SessionId);
        CheckAlive(session);
        if (session.State.State == WindowEmbeddingState.TargetExited)
            return Result(session, NativeWindowResult.Ok(), "Target exited; no reused window was modified.");
        if (notification.Kind == WindowLifecycleEventKind.LocationChanged && session.State.State == WindowEmbeddingState.Embedded)
        {
            // Explicitly placed (canvas) sessions verify the target size instead of refilling the host.
            if (session.ExplicitlyPlaced && session.LastNative is { } expected)
                return VerifyExternalSize(session, expected);
            return Resize(session.Id);
        }
        return Result(session, NativeWindowResult.Ok(), "Duplicate, delayed or informational event checked.");
    }

    private EmbeddingResult VerifyExternalSize(Session session, WindowBounds expected)
    {
        WindowBounds current = default;
        var read = Invoke(session, () => api.GetTargetBounds(session.Snapshot.Identity.Hwnd, session.Host, out current));
        if (!read.Success) return Result(session, read, "External size read-back failed; keeping the last placement.");
        // Echoes of our own placement are within a small tolerance and are ignored, preventing feedback loops.
        if (Math.Abs(current.Width - expected.Width) <= 2 && Math.Abs(current.Height - expected.Height) <= 2)
            return Result(session, NativeWindowResult.Ok(), "Location change matches the last explicit placement; ignored.");
        session.External = current;
        session.LastNative = current;
        Log(session, DiagnosticEvent.NativeRead, NativeWindowResult.Ok());
        return Result(session, NativeWindowResult.Ok(),
            "External target size change read back; the shared layout will sync on the next refresh.");
    }

    private NativeWindowResult ResizeNative(Session session)
    {
        WindowBounds bounds = default;
        var result = Invoke(session, () => api.GetClientBounds(session.Host, out bounds));
        if (result.Success && (bounds.Width <= 0 || bounds.Height <= 0)) result = NativeWindowResult.Fail(OperationCodes.InvalidArgument);
        if (result.Success) result = Invoke(session, () => api.SetBounds(session.Snapshot.Identity.Hwnd, bounds));
        return result;
    }

    private NativeWindowResult Restore(Session session)
    {
        if (!CheckAlive(session)) return NativeWindowResult.Ok();
        var snapshot = session.Snapshot;
        var parent = Invoke(session, () => api.SetParent(snapshot.Identity.Hwnd, snapshot.Parent));
        if (!parent.Success) return GoneOrFailed(session, parent);
        var styles = Invoke(session, () => api.SetStyles(snapshot.Identity.Hwnd, snapshot.Style, snapshot.ExtendedStyle));
        var placement = Invoke(session, () => api.RestorePlacement(snapshot.Identity.Hwnd, snapshot.Placement));
        var bounds = snapshot.Placement.ShowCommand == 3 ? NativeWindowResult.Ok()
            : Invoke(session, () => api.SetBounds(snapshot.Identity.Hwnd, snapshot.Bounds));
        if (!CheckAlive(session)) return NativeWindowResult.Ok();
        if (!styles.Success) return styles;
        if (!placement.Success) return placement;
        if (!bounds.Success) return bounds;
        return Invoke(session, () => api.VerifyRestored(snapshot));
    }

    private NativeWindowResult GoneOrFailed(Session session, NativeWindowResult failed) => CheckAlive(session) ? failed : NativeWindowResult.Ok();
    private bool CheckAlive(Session session)
    {
        if (!session.NeedsRestore) return session.State.State != WindowEmbeddingState.TargetExited;
        bool alive;
        try { alive = api.IsAlive(session.Snapshot); }
        catch (Exception error)
        {
            diagnostics.Record(new(DiagnosticEvent.NativeRead, DiagnosticLevel.Error, session.Id,
                DiagnosticCode.InternalError, ExceptionHResult: error.HResult));
            // An unreadable identity is not proof of death. Keep the recovery state.
            return true;
        }
        if (alive) return true;
        session.State.TryTransition(WindowEmbeddingState.TargetExited);
        session.NeedsRestore = false;
        session.Last = NativeWindowResult.Fail(OperationCodes.WindowNotFound);
        Log(session, DiagnosticEvent.TargetExited, NativeWindowResult.Ok());
        return false;
    }
    private NativeWindowResult Invoke(Session session, Func<NativeWindowResult> action)
    {
        try { return api.IsAlive(session.Snapshot) ? action() : NativeWindowResult.Fail(OperationCodes.WindowNotFound); }
        catch (Exception error)
        {
            diagnostics.Record(new(DiagnosticEvent.NativeRead, DiagnosticLevel.Error, session.Id,
                DiagnosticCode.InternalError, ExceptionHResult: error.HResult));
            return NativeWindowResult.Fail(OperationCodes.InternalError);
        }
    }
    private void Log(Session session, DiagnosticEvent eventName, NativeWindowResult result) =>
        diagnostics.Record(new(eventName, result.Success ? DiagnosticLevel.Information : DiagnosticLevel.Error,
            session.Id, result.Success ? DiagnosticCode.Ok : DiagnosticCode.NativeFailure, result.Api, result.Error,
            session.Snapshot.Identity.ProcessId, session.Snapshot.Identity.Hwnd));
    private EmbeddingResult Result(Session session, NativeWindowResult result, string message)
    {
        session.Last = result;
        return new(result.Success, result.Code, message, session.Id, session.View, result.Api, result.Error, session.Recovery);
    }
    private EmbeddingResult Failure(Guid id, Exception error)
    {
        diagnostics.Record(new(DiagnosticEvent.Embedding, DiagnosticLevel.Error, id, DiagnosticCode.InternalError, ExceptionHResult: error.HResult));
        return new(false, OperationCodes.InternalError, "Native capture failed; no mutation began.", id, null);
    }
    private static EmbeddingResult Missing(Guid id, EmbeddingSessionStatus? session = null) =>
        new(false, OperationCodes.WindowNotFound, "No embedded target for this session.", id, session);
}
