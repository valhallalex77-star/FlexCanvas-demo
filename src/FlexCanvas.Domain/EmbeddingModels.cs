namespace FlexCanvas.Domain;

/// <summary>Production lifecycle; prototype contracts remain independent.</summary>
public enum WindowEmbeddingState
{
    Discovered, Validating, Embedding, Embedded, RollingBack, Failed,
    Detaching, DetachFailed, Detached, TargetExited
}

/// <summary>Validates transitions without exposing a writable state.</summary>
public sealed class WindowEmbeddingStateMachine
{
    public WindowEmbeddingState State { get; private set; }
    public bool TryTransition(WindowEmbeddingState next)
    {
        bool allowed = (State, next) switch
        {
            (WindowEmbeddingState.Discovered, WindowEmbeddingState.Validating) => true,
            (WindowEmbeddingState.Validating, WindowEmbeddingState.Embedding or WindowEmbeddingState.Failed) => true,
            (WindowEmbeddingState.Embedding, WindowEmbeddingState.Embedded or WindowEmbeddingState.RollingBack or WindowEmbeddingState.TargetExited) => true,
            (WindowEmbeddingState.RollingBack, WindowEmbeddingState.Failed or WindowEmbeddingState.TargetExited) => true,
            (WindowEmbeddingState.Embedded, WindowEmbeddingState.Detaching or WindowEmbeddingState.TargetExited) => true,
            (WindowEmbeddingState.Detaching, WindowEmbeddingState.Detached or WindowEmbeddingState.DetachFailed or WindowEmbeddingState.TargetExited) => true,
            (WindowEmbeddingState.DetachFailed or WindowEmbeddingState.Failed, WindowEmbeddingState.Detaching or WindowEmbeddingState.TargetExited) => true,
            _ => false
        };
        if (allowed) State = next;
        return allowed;
    }
}

/// <summary>Original native placement data; coordinates follow WINDOWPLACEMENT semantics.</summary>
public readonly record struct WindowPlacement(uint Flags, uint ShowCommand, int MinX, int MinY,
    int MaxX, int MaxY, WindowBounds NormalBounds);

/// <summary>Runtime-only recovery snapshot; never used as a workspace identity.</summary>
public sealed record WindowSnapshot(WindowIdentity Identity, long ProcessCreationTime, nint Parent,
    long Style, long ExtendedStyle, WindowBounds Bounds, WindowPlacement Placement, uint Dpi);

/// <summary>Structured native result, containing no window text or arbitrary exception messages.</summary>
public sealed record NativeWindowResult(bool Success, string Code, NativeApi Api = NativeApi.None, int? Error = null)
{
    public static NativeWindowResult Ok() => new(true, OperationCodes.Ok);
    public static NativeWindowResult Fail(string code, NativeApi api = NativeApi.None, int? error = null) => new(false, code, api, error);
}

/// <summary>Immutable production session projection; ExternalBounds reports an externally changed target size awaiting model sync.</summary>
public sealed record EmbeddingSessionStatus(Guid SessionId, nint Host, WindowSnapshot Original,
    WindowEmbeddingState State, bool NeedsRestore, NativeWindowResult LastResult,
    NativeWindowResult? RecoveryResult = null, WindowBounds? ExternalBounds = null);

/// <summary>Operation and recovery results stay separate to avoid disguising rollback failures.</summary>
public sealed record EmbeddingResult(bool Success, string Code, string Message, Guid? SessionId,
    EmbeddingSessionStatus? Session, NativeApi Api = NativeApi.None, int? Win32ErrorCode = null,
    NativeWindowResult? RecoveryResult = null)
{
    public string CorrelationId => SessionId?.ToString("N") ?? "";
}

public enum WindowLifecycleEventKind { Destroyed, LocationChanged, ForegroundChanged, ProcessExited }
public sealed record WindowLifecycleEvent(Guid SessionId, WindowIdentity Identity, WindowLifecycleEventKind Kind);
