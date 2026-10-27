using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace FlexCanvas.Infrastructure.Win32;

/// <summary>A dedicated STA message-pump thread; no target operation runs on the UI thread.</summary>
public sealed class WindowHostDispatcher : IAsyncDisposable
{
    private readonly TaskCompletionSource<Dispatcher> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private int _disposed;

    /// <summary>Starts the serialized native host thread.</summary>
    public WindowHostDispatcher()
    {
        _thread = new Thread(() =>
        {
            _ready.SetResult(Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        }) { IsBackground = true, Name = "FlexCanvas.WindowHost" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    /// <summary>Queues one synchronous operation; cancellation is honored before it starts.</summary>
    public async Task<T> InvokeAsync<T>(Func<T> operation, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var dispatcher = await _ready.Task.ConfigureAwait(false);
        return await dispatcher.InvokeAsync(() =>
        {
            // Dispatcher callbacks can inherit a caller's DPI context; normalize each operation.
            nint previous = NativeMethods.SetThreadDpiAwarenessContext(-4);
            if (previous == 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "SetThreadDpiAwarenessContext failed.");
            try { return operation(); }
            finally { NativeMethods.SetThreadDpiAwarenessContext(previous); }
        }, DispatcherPriority.Normal, cancellationToken).Task.ConfigureAwait(false);
    }

    /// <summary>Stops the pump without blocking the WPF caller thread.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        var dispatcher = await _ready.Task.ConfigureAwait(false);
        dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
        await Task.Run(_thread.Join).ConfigureAwait(false);
    }
}
