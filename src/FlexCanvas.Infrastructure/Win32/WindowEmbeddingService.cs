using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using FlexCanvas.Application;
using FlexCanvas.Domain;

namespace FlexCanvas.Infrastructure.Win32;

/// <summary>Serializes production mutations and dispatches minimal out-of-context notifications.</summary>
public sealed class WindowEmbeddingService : IWindowEmbeddingService, IAsyncDisposable
{
    private sealed class Watch
    {
        internal required EmbeddingSessionStatus Session;
        internal required NativeMethods.WinEventProc Callback;
        internal readonly List<nint> Hooks = [];
        internal Process? Process;
        internal EventHandler? ExitHandler;
    }
    private readonly WindowHostDispatcher _dispatcher;
    private readonly WindowEmbeddingEngine _engine;
    private readonly IDiagnosticSink _diagnostics;
    private readonly Dictionary<Guid, Watch> _watches = [];
    private readonly Channel<WindowLifecycleEvent> _events = Channel.CreateBounded<WindowLifecycleEvent>(
        new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly Task _consumer;
    private int _stopping;
    internal int RegisteredHookCount => _watches.Values.Sum(w => w.Hooks.Count);

    public WindowEmbeddingService(WindowHostDispatcher dispatcher, IWindowEmbeddingApi api, IDiagnosticSink diagnostics)
    {
        _dispatcher = dispatcher;
        _engine = new(api, diagnostics);
        _diagnostics = diagnostics;
        _consumer = Task.Run(ConsumeAsync);
    }

    public Task<EmbeddingResult> EmbedAsync(WindowIdentity target, nint host, CancellationToken cancellationToken = default) =>
        RunAsync(() =>
        {
            var result = _engine.Embed(target, host);
            if (result.Success && result.Session is { NeedsRestore: true } status)
            {
                try { AttachWatch(status); }
                catch (Exception error)
                {
                    _diagnostics.Record(new(DiagnosticEvent.WindowEvent, DiagnosticLevel.Error, status.SessionId,
                        DiagnosticCode.NativeFailure, NativeApi.SetWinEventHook, (error as Win32Exception)?.NativeErrorCode,
                        ExceptionHResult: error.HResult));
                    var recovery = _engine.Detach(status.SessionId);
                    result = new(false, OperationCodes.EmbeddingFailed, "Monitoring could not start; recovery attempted. Keep host if recovery failed.",
                        status.SessionId, recovery.Session, NativeApi.SetWinEventHook, (error as Win32Exception)?.NativeErrorCode,
                        recovery.Session?.RecoveryResult);
                }
            }
            CleanupWatches();
            return result;
        }, cancellationToken);
    public Task<EmbeddingResult> ResizeAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        RunAsync(() => _engine.Resize(sessionId), cancellationToken);
    public Task<EmbeddingResult> PlaceAsync(Guid sessionId, WindowBounds explicitBounds, CancellationToken cancellationToken = default) =>
        RunAsync(() => _engine.Place(sessionId, explicitBounds), cancellationToken);
    public Task<EmbeddingResult> FocusAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        RunAsync(() => _engine.Focus(sessionId), cancellationToken);
    public Task<EmbeddingResult> DetachAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        RunAsync(() => { var result = _engine.Detach(sessionId); CleanupWatches(); return result; }, cancellationToken);
    public Task<EmbeddingResult> HandleEventAsync(WindowLifecycleEvent notification, CancellationToken cancellationToken = default) =>
        RunAsync(() => { var result = _engine.HandleEvent(notification); CleanupWatches(); return result; }, cancellationToken);
    public async Task<IReadOnlyList<EmbeddingSessionStatus>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
        await _dispatcher.InvokeAsync(() => { var states = _engine.GetSessions(); CleanupWatches(); return states; }, cancellationToken).ConfigureAwait(false);

    private async Task<EmbeddingResult> RunAsync(Func<EmbeddingResult> operation, CancellationToken token)
    {
        if (Volatile.Read(ref _stopping) != 0) return new(false, OperationCodes.Cancelled, "Service stopping.", null, null);
        try
        {
            return await _dispatcher.InvokeAsync(() => Volatile.Read(ref _stopping) == 0 ? operation()
                : new(false, OperationCodes.Cancelled, "Service stopping; operation not started.", null, null), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return new(false, OperationCodes.Cancelled, "Cancelled before native mutation began.", null, null); }
        catch (Exception error)
        {
            Guid id = Guid.NewGuid();
            _diagnostics.Record(new(DiagnosticEvent.NativeRead, DiagnosticLevel.Error, id,
                DiagnosticCode.InternalError, ExceptionHResult: error.HResult));
            return new(false, OperationCodes.InternalError, "Operation failed; query sessions and keep the host open.", id, null);
        }
    }
    private void AttachWatch(EmbeddingSessionStatus session)
    {
        var watch = new Watch
        {
            Session = session,
            Callback = (_, eventId, hwnd, objectId, childId, _, _) =>
            {
                if (hwnd != session.Original.Identity.Hwnd || objectId != 0 || childId != 0) return;
                var kind = eventId switch
                {
                    0x8001 => WindowLifecycleEventKind.Destroyed,
                    0x800B => WindowLifecycleEventKind.LocationChanged,
                    3 => WindowLifecycleEventKind.ForegroundChanged,
                    _ => (WindowLifecycleEventKind?)null
                };
                if (kind is not null) Queue(new(session.SessionId, session.Original.Identity, kind.Value));
            }
        };
        _watches.Add(session.SessionId, watch);
        // Retain the delegate even when a partial registration cannot yet be unhooked.
        foreach (var range in new[] { (0x8001u, 0x8001u), (0x800Bu, 0x800Bu), (3u, 3u) })
        {
            NativeMethods.SetLastError(0);
            nint hook = NativeMethods.SetWinEventHook(range.Item1, range.Item2, 0, watch.Callback,
                session.Original.Identity.ProcessId, 0, 2);
            if (hook == 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "SetWinEventHook failed.");
            watch.Hooks.Add(hook);
        }
        watch.Process = Process.GetProcessById((int)session.Original.Identity.ProcessId);
        watch.ExitHandler = (_, _) => Queue(new(session.SessionId, session.Original.Identity, WindowLifecycleEventKind.ProcessExited));
        watch.Process.Exited += watch.ExitHandler;
        watch.Process.EnableRaisingEvents = true;
        if (watch.Process.HasExited) Queue(new(session.SessionId, session.Original.Identity, WindowLifecycleEventKind.ProcessExited));
    }
    private void Queue(WindowLifecycleEvent notification)
    {
        if (Volatile.Read(ref _stopping) != 0) return;
        if (!_events.Writer.TryWrite(notification))
            _diagnostics.Record(new(DiagnosticEvent.WindowEvent, DiagnosticLevel.Warning, notification.SessionId, DiagnosticCode.NativeFailure));
    }
    private async Task ConsumeAsync()
    {
        await foreach (var notification in _events.Reader.ReadAllAsync())
        {
            try
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    _engine.HandleEvent(notification);
                    CleanupWatches();
                    return true;
                }).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                _diagnostics.Record(new(DiagnosticEvent.WindowEvent, DiagnosticLevel.Error, notification.SessionId,
                    DiagnosticCode.InternalError, ExceptionHResult: error.HResult));
            }
        }
    }
    private void CleanupWatches()
    {
        var active = _engine.GetSessions().Where(s => s.NeedsRestore).Select(s => s.SessionId).ToHashSet();
        foreach (var watch in _watches.Values.Where(w => !active.Contains(w.Session.SessionId)).ToArray())
            if (ReleaseWatch(watch)) _watches.Remove(watch.Session.SessionId);
    }
    private bool ReleaseWatch(Watch watch)
    {
        foreach (nint hook in watch.Hooks.ToArray())
        {
            NativeMethods.SetLastError(0);
            if (NativeMethods.UnhookWinEvent(hook)) watch.Hooks.Remove(hook);
            else _diagnostics.Record(new(DiagnosticEvent.WindowEvent, DiagnosticLevel.Error, watch.Session.SessionId,
                DiagnosticCode.NativeFailure, NativeApi.UnhookWinEvent, Marshal.GetLastPInvokeError()));
        }
        if (watch.Process is not null)
        {
            watch.Process.Exited -= watch.ExitHandler;
            watch.Process.Dispose();
            watch.Process = null;
        }
        return watch.Hooks.Count == 0;
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _stopping, 1, 0) != 0) return;
        try
        {
            await _dispatcher.InvokeAsync(() =>
            {
                foreach (var session in _engine.GetSessions().Where(s => s.NeedsRestore))
                {
                    var result = _engine.Detach(session.SessionId);
                    if (!result.Success) throw new InvalidOperationException("Recovery failed; retain service and host.");
                }
                CleanupWatches();
                if (_watches.Count != 0) throw new InvalidOperationException("Unhook failed; retain callback delegates and retry.");
                return true;
            }).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Exchange(ref _stopping, 0);
            throw;
        }
        _events.Writer.TryComplete();
        await _consumer.ConfigureAwait(false);
    }
}
