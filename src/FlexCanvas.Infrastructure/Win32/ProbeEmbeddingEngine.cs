using FlexCanvas.Application.Poc;
using FlexCanvas.Domain.Poc;

namespace FlexCanvas.Infrastructure.Win32;

internal sealed class ProbeEmbeddingEngine(IProbeWindowApi api)
{
    private EmbeddingStateMachine _state = new();
    private ProbeSnapshot? _snapshot;
    private nint _host;
    private Guid _sessionId;
    private bool _needsRestore;
    private ProbeResult _last = ProbeResult.Ok("No active probe.");

    internal ProbeStatus? Status => _snapshot is null ? null : new(_sessionId, _snapshot, _state.State, _last);

    internal ProbeResult Embed(nint target, nint host)
    {
        Poll();
        if (_needsRestore)
            return _snapshot!.Hwnd == target && _host == host && _state.State == EmbeddingState.Embedded
                ? ProbeResult.Ok("Already embedded.") : ProbeResult.Fail("INVALID_ARGUMENT", "Detach the existing session first.");

        _state = new();
        _snapshot = null;
        _state.TryTransition(EmbeddingState.Validating);
        _last = api.Capture(target, host, out var snapshot);
        if (!_last.Success)
        {
            _state.TryTransition(EmbeddingState.Failed);
            return _last;
        }

        _snapshot = snapshot!;
        _host = host;
        _sessionId = Guid.NewGuid();
        _state.TryTransition(EmbeddingState.Embedding);
        _needsRestore = true;
        // Explicitly remove POPUP/caption/frame controls and add CHILD.
        long childStyle = (_snapshot.Style & ~(0x80000000L | 0x00C00000L | 0x00040000L | 0x00030000L)) | 0x40000000L;
        var result = InvokeNative(() => api.SetStyles(target, childStyle, _snapshot.ExtendedStyle));
        if (result.Success) result = InvokeNative(() => api.SetParent(target, host));
        if (result.Success)
        {
            ProbeBounds bounds = default;
            result = InvokeNative(() => api.GetClientBounds(host, out bounds));
            if (result.Success) result = InvokeNative(() => api.SetBounds(target, bounds));
        }
        if (result.Success)
        {
            _state.TryTransition(EmbeddingState.Embedded);
            return _last = ProbeResult.Ok("Embedded; verify input manually before claiming compatibility.");
        }

        _state.TryTransition(EmbeddingState.RollingBack);
        var rollback = Restore();
        _state.TryTransition(EmbeddingState.Failed);
        _needsRestore = !rollback.Success;
        return _last = ProbeResult.Fail(result.Code,
            $"{result.Message} Rollback: {(rollback.Success ? "restored" : rollback.Message)}",
            result.Win32Error ?? rollback.Win32Error);
    }

    internal ProbeResult Resize()
    {
        Poll();
        if (!_needsRestore || _state.State != EmbeddingState.Embedded)
            return ProbeResult.Fail("WINDOW_NOT_FOUND", "No embedded probe.");
        ProbeBounds bounds = default;
        var result = InvokeNative(() => api.GetClientBounds(_host, out bounds));
        if (result.Success) result = InvokeNative(() => api.SetBounds(_snapshot!.Hwnd, bounds));
        return _last = result;
    }

    internal ProbeResult Detach()
    {
        Poll();
        if (!_needsRestore) return ProbeResult.Ok("Already detached or target exited.");
        if (!_state.TryTransition(EmbeddingState.Detaching))
            return ProbeResult.Fail("INVALID_ARGUMENT", "Session cannot detach in its current state.");
        var result = Restore();
        _state.TryTransition(result.Success ? EmbeddingState.Detached : EmbeddingState.DetachFailed);
        _needsRestore = !result.Success;
        return _last = result;
    }

    internal ProbeStatus? Poll()
    {
        if (_needsRestore && !api.IsAlive(_snapshot!.Hwnd, _snapshot.ProcessId))
        {
            _state.TryTransition(EmbeddingState.TargetExited);
            _needsRestore = false;
            _last = ProbeResult.Ok("Target exited; session cleaned without modifying another window.");
        }
        return Status;
    }

    private ProbeResult Restore()
    {
        if (!api.IsAlive(_snapshot!.Hwnd, _snapshot.ProcessId))
            return ProbeResult.Ok("Target no longer exists.");
        var parent = InvokeNative(() => api.SetParent(_snapshot.Hwnd, _snapshot.Parent));
        if (!parent.Success) return ProbeResult.Fail("DETACH_FAILED", parent.Message, parent.Win32Error);
        var style = InvokeNative(() => api.SetStyles(_snapshot.Hwnd, _snapshot.Style, _snapshot.ExtendedStyle));
        var bounds = InvokeNative(() => api.SetBounds(_snapshot.Hwnd, _snapshot.Bounds));
        var failed = !style.Success ? style : bounds;
        return failed.Success ? ProbeResult.Ok("Restored original parent, styles and bounds.")
            : ProbeResult.Fail("DETACH_FAILED", failed.Message, failed.Win32Error);
    }

    private ProbeResult InvokeNative(Func<ProbeResult> operation)
    {
        try
        {
            if (!api.IsAlive(_snapshot!.Hwnd, _snapshot.ProcessId))
                return ProbeResult.Fail("WINDOW_NOT_FOUND", "Target identity changed before the native operation.");
            return operation();
        }
        catch (Exception error)
        {
            return ProbeResult.Fail("EMBEDDING_FAILED", $"Native adapter exception: {error.GetType().Name}.");
        }
    }
}
