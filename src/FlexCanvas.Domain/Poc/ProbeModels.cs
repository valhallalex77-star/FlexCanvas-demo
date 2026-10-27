namespace FlexCanvas.Domain.Poc;

/// <summary>Result contract for the prototype; production service contracts remain unfrozen.</summary>
public sealed record ProbeResult(bool Success, string Code, string Message, int? Win32Error = null)
{
    /// <summary>Creates a successful result.</summary>
    public static ProbeResult Ok(string message = "Completed.") => new(true, "OK", message);

    /// <summary>Creates an explicit failure result.</summary>
    public static ProbeResult Fail(string code, string message, int? error = null) => new(false, code, message, error);
}

/// <summary>Physical pixel coordinates, not persisted workspace DIP values.</summary>
public readonly record struct ProbeBounds(int X, int Y, int Width, int Height);

/// <summary>Original state needed for a controlled probe's best-effort restoration.</summary>
public sealed record ProbeSnapshot(nint Hwnd, uint ProcessId, nint Parent, long Style, long ExtendedStyle,
    ProbeBounds Bounds, uint Dpi);

/// <summary>A candidate controlled probe window; this is not a compatibility guarantee.</summary>
public sealed record ProbeCandidate(nint Hwnd, uint ProcessId, string Kind);

/// <summary>Immutable session status exposed to the UI.</summary>
public sealed record ProbeStatus(Guid SessionId, ProbeSnapshot Original, EmbeddingState State, ProbeResult LastResult);

/// <summary>Full lifecycle vocabulary from specification section 9.9.</summary>
public enum EmbeddingState
{
    Discovered,
    Validating,
    Embedding,
    Embedded,
    RollingBack,
    Failed,
    Detaching,
    DetachFailed,
    Detached,
    TargetExited
}

/// <summary>Rejects illegal session transitions without changing the current state.</summary>
public sealed class EmbeddingStateMachine
{
    /// <summary>Gets the current state.</summary>
    public EmbeddingState State { get; private set; } = EmbeddingState.Discovered;

    /// <summary>Attempts one allowed transition.</summary>
    public bool TryTransition(EmbeddingState next)
    {
        bool allowed = (State, next) switch
        {
            (EmbeddingState.Discovered, EmbeddingState.Validating) => true,
            (EmbeddingState.Validating, EmbeddingState.Embedding or EmbeddingState.Failed) => true,
            (EmbeddingState.Embedding, EmbeddingState.Embedded or EmbeddingState.RollingBack) => true,
            (EmbeddingState.RollingBack, EmbeddingState.Failed) => true,
            (EmbeddingState.Embedded, EmbeddingState.Detaching or EmbeddingState.TargetExited) => true,
            (EmbeddingState.Detaching, EmbeddingState.Detached or EmbeddingState.DetachFailed or EmbeddingState.TargetExited) => true,
            (EmbeddingState.DetachFailed, EmbeddingState.Detaching or EmbeddingState.TargetExited) => true,
            (EmbeddingState.Failed, EmbeddingState.Detaching or EmbeddingState.TargetExited) => true,
            _ => false
        };
        if (allowed) State = next;
        return allowed;
    }
}
