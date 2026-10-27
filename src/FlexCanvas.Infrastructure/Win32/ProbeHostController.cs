using FlexCanvas.Domain.Poc;

namespace FlexCanvas.Infrastructure.Win32;

/// <summary>Asynchronous POC facade coordinating a single controlled probe session.</summary>
public sealed class ProbeHostController : IAsyncDisposable
{
    private readonly Win32ProbeWindowApi _api;
    private readonly ProbeEmbeddingEngine _engine;
    private readonly WindowHostDispatcher _dispatcher = new();

    /// <summary>Restricts discovery and capture to probes built under this directory.</summary>
    public ProbeHostController(string probeRoot)
    {
        _api = new(probeRoot);
        _engine = new(_api);
    }

    /// <summary>Discovers controlled probes on demand, not by desktop polling.</summary>
    public Task<IReadOnlyList<ProbeCandidate>> DiscoverAsync() => _dispatcher.InvokeAsync(_api.Discover);
    /// <summary>Embeds a selected controlled HWND and saves its original state.</summary>
    public Task<ProbeResult> EmbedAsync(nint target, nint host) => _dispatcher.InvokeAsync(() => _engine.Embed(target, host));
    /// <summary>Synchronizes the target with the current host client area.</summary>
    public Task<ProbeResult> ResizeAsync() => _dispatcher.InvokeAsync(_engine.Resize);
    /// <summary>Restores or reports the retained session without killing the target.</summary>
    public Task<ProbeResult> DetachAsync() => _dispatcher.InvokeAsync(_engine.Detach);
    /// <summary>Checks only the active target handle and PID, not all desktop windows.</summary>
    public Task<ProbeStatus?> GetStatusAsync() => _dispatcher.InvokeAsync(_engine.Poll);

    /// <summary>Restores the target before ending the serialized worker; failure remains explicit.</summary>
    public async ValueTask DisposeAsync()
    {
        var result = await DetachAsync().ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException($"Cannot dispose an unrestored probe: {result.Message}");
        await _dispatcher.DisposeAsync().ConfigureAwait(false);
    }
}
