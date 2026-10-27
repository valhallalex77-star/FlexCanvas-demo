using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using FlexCanvas.Application;
using FlexCanvas.Domain;
using FlexCanvas.Infrastructure.Win32;
using Xunit;
using Xunit.Abstractions;

namespace FlexCanvas.Infrastructure.Tests;

[Collection("Native probes")]
public sealed class ProductionEmbeddingTests(ITestOutputHelper output)
{
    [Theory]
    [Trait("Category", "NativeIntegration")]
    [InlineData("Win32")]
    [InlineData("Wpf")]
    [InlineData("WinForms")]
    public async Task ProductionServiceEmbedsRestoresAndHandlesActualExitEvents(string kind)
    {
        using var process = StartProbe(kind);
        await using var dispatcher = new WindowHostDispatcher();
        var sink = new Sink();
        var api = new Win32WindowEmbeddingApi();
        var service = new WindowEmbeddingService(dispatcher, api, sink);
        nint host = 0, target = 0;
        try
        {
            target = await WaitForWindow(process);
            host = await dispatcher.InvokeAsync(() => CreateHost(800, 600));
            var identity = new WindowIdentity(target, (uint)process.Id);
            WindowSnapshot? original = null;
            Assert.True((await dispatcher.InvokeAsync(() => api.Capture(identity, host, out original))).Success);
            Assert.True(original!.ProcessCreationTime > 0);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                var result = await service.EmbedAsync(identity, host);
                Assert.True(result.Success, $"{kind}: {result.Code}/{result.Api}/{result.Win32ErrorCode}");
                Assert.Equal(WindowEmbeddingState.Embedded, result.Session!.State);
                Guid id = result.SessionId!.Value;
                Assert.Equal(3, await dispatcher.InvokeAsync(() => service.RegisteredHookCount));
                await dispatcher.InvokeAsync(() =>
                {
                    Assert.Equal(host, NativeMethods.GetParent(target));
                    Assert.True(api.SetBounds(host, new(200 + cycle * 10, 150, 900, 650)).Success);
                    return true;
                });
                Assert.True((await service.ResizeAsync(id)).Success);
                await dispatcher.InvokeAsync(() =>
                {
                    Assert.True(NativeMethods.GetClientRect(host, out var client));
                    var origin = new NativeMethods.Point();
                    Assert.True(NativeMethods.ClientToScreen(host, ref origin));
                    Assert.True(NativeMethods.GetWindowRect(target, out var actual));
                    Assert.Equal(origin.X, actual.Left);
                    Assert.Equal(origin.Y, actual.Top);
                    Assert.Equal(client.Right, actual.Right - actual.Left);
                    Assert.Equal(client.Bottom, actual.Bottom - actual.Top);
                    return true;
                });
                var detached = await service.DetachAsync(id);
                await dispatcher.InvokeAsync(() =>
                {
                    NativeMethods.GetWindowRect(target, out var actual);
                    Win32ProbeWindowApi.ReadStyle(target, -16, out long style);
                    Win32ProbeWindowApi.ReadStyle(target, -20, out long extended);
                    output.WriteLine($"Detach {kind}: {detached.Code}/{detached.Api}/{detached.Win32ErrorCode}; "
                        + $"style={style:X}/{original.Style:X}, ex={extended:X}/{original.ExtendedStyle:X}; "
                        + $"rect={actual.Left},{actual.Top},{actual.Right - actual.Left},{actual.Bottom - actual.Top}; expected={original.Bounds}");
                    return true;
                });
                Assert.True(detached.Success, $"{detached.Code}/{detached.Api}/{detached.Win32ErrorCode}");
                Assert.True((await service.DetachAsync(id)).Success);
                Assert.Equal(0, await dispatcher.InvokeAsync(() => service.RegisteredHookCount));
                Assert.True((await dispatcher.InvokeAsync(() => api.VerifyRestored(original))).Success);
                Assert.False(process.HasExited);
            }
            var embedded = await service.EmbedAsync(identity, host);
            Assert.True(embedded.Success);
            Assert.True(NativeMethods.PostMessage(target, 0x10, 0, 0));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(timeout.Token);
            // Do not call GetSessions here: the real WinEvent/Process callback must drive convergence.
            await WaitUntil(() => sink.Entries.Any(e => e.Event == DiagnosticEvent.TargetExited && e.CorrelationId == embedded.SessionId));
            Assert.Equal(0, await dispatcher.InvokeAsync(() => service.RegisteredHookCount));
            var status = Assert.Single(await service.GetSessionsAsync(), s => s.SessionId == embedded.SessionId);
            Assert.Equal(WindowEmbeddingState.TargetExited, status.State);
            Assert.False(status.NeedsRestore);
        }
        finally
        {
            try { await CloseOwned(process, target); await service.DisposeAsync(); }
            finally
            {
                await CloseOwned(process, target);
                if (host != 0) await dispatcher.InvokeAsync(() => NativeMethods.DestroyWindow(host));
            }
        }
    }

    [Fact]
    [Trait("Category", "NativeIntegration")]
    public async Task EmptyHostRollsBackAndInvalidIdentityAndCancellationNeverMutate()
    {
        using var process = StartProbe("Win32");
        await using var dispatcher = new WindowHostDispatcher();
        var api = new Win32WindowEmbeddingApi();
        var service = new WindowEmbeddingService(dispatcher, api, new Sink());
        nint host = 0, target = 0;
        try
        {
            target = await WaitForWindow(process);
            host = await dispatcher.InvokeAsync(() => CreateHost(0, 0));
            var identity = new WindowIdentity(target, (uint)process.Id);
            WindowSnapshot? original = null;
            Assert.True((await dispatcher.InvokeAsync(() => api.Capture(identity, host, out original))).Success);
            var failed = await service.EmbedAsync(identity, host);
            Assert.False(failed.Success);
            Assert.True(failed.RecoveryResult!.Success);
            Assert.False(failed.Session!.NeedsRestore);
            Assert.True((await dispatcher.InvokeAsync(() => api.VerifyRestored(original!))).Success);
            Assert.False((await service.EmbedAsync(new(target, (uint)process.Id + 1), host)).Success);
            Assert.False((await service.EmbedAsync(new(host, (uint)Environment.ProcessId), host)).Success);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Equal(OperationCodes.Cancelled, (await service.EmbedAsync(identity, host, cancellation.Token)).Code);
            Assert.True((await dispatcher.InvokeAsync(() => api.VerifyRestored(original!))).Success);
        }
        finally
        {
            try { await CloseOwned(process, target); await service.DisposeAsync(); }
            finally
            {
                await CloseOwned(process, target);
                if (host != 0) await dispatcher.InvokeAsync(() => NativeMethods.DestroyWindow(host));
            }
        }
    }

    [Fact]
    [Trait("Category", "NativeIntegration")]
    public async Task ConcurrentRequestsAndServiceShutdownRestoreMultipleTargets()
    {
        using var first = StartProbe("Win32");
        using var second = StartProbe("Win32");
        await using var dispatcher = new WindowHostDispatcher();
        var api = new Win32WindowEmbeddingApi();
        var service = new WindowEmbeddingService(dispatcher, api, new Sink());
        nint a = 0, b = 0, hostA = 0, hostB = 0;
        try
        {
            a = await WaitForWindow(first); b = await WaitForWindow(second);
            hostA = await dispatcher.InvokeAsync(() => CreateHost(800, 600));
            hostB = await dispatcher.InvokeAsync(() => CreateHost(800, 600));
            var results = await Task.WhenAll(service.EmbedAsync(new(a, (uint)first.Id), hostA),
                service.EmbedAsync(new(b, (uint)second.Id), hostB));
            Assert.All(results, r => Assert.True(r.Success));
            Assert.Equal(6, await dispatcher.InvokeAsync(() => service.RegisteredHookCount));
            Guid id = results[0].SessionId!.Value;
            var races = await Task.WhenAll(service.ResizeAsync(id), service.DetachAsync(id), service.ResizeAsync(id));
            Assert.True(races[1].Success);
            await service.DisposeAsync();
            foreach (var result in results)
                Assert.True((await dispatcher.InvokeAsync(() => api.VerifyRestored(result.Session!.Original))).Success);
            Assert.False(first.HasExited);
            Assert.False(second.HasExited);
            Assert.Equal(0, await dispatcher.InvokeAsync(() => service.RegisteredHookCount));
        }
        finally
        {
            try { await CloseOwned(first, a); await CloseOwned(second, b); await service.DisposeAsync(); }
            finally
            {
                await CloseOwned(first, a); await CloseOwned(second, b);
                if (hostA != 0) await dispatcher.InvokeAsync(() => NativeMethods.DestroyWindow(hostA));
                if (hostB != 0) await dispatcher.InvokeAsync(() => NativeMethods.DestroyWindow(hostB));
            }
        }
    }
    private static nint CreateHost(int width, int height)
    {
        nint host = NativeMethods.CreateWindowEx(0, "STATIC", "S2 owned test host", 0x90000000,
            150, 150, width, height, 0, 0, 0, 0);
        Assert.NotEqual((nint)0, host);
        return host;
    }
    private static Process StartProbe(string kind)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        string configuration = root.Parent!.Name;
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "FlexCanvas.sln"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Checkout not found.");
        string exe = Path.Combine(root.FullName, "tools", "Probes", $"{kind}Probe", "bin", configuration,
            "net8.0-windows", $"FlexCanvas.{kind}Probe.exe");
        return Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false })!;
    }
    private static async Task<nint> WaitForWindow(Process process)
    {
        nint handle = 0;
        await WaitUntil(() => { process.Refresh(); handle = process.MainWindowHandle; return handle != 0; });
        return handle;
    }
    private static async Task WaitUntil(Func<bool> predicate)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (predicate()) return;
            await Task.Delay(100);
        }
        throw new TimeoutException("Owned test condition timed out.");
    }
    private static async Task CloseOwned(Process process, nint handle)
    {
        if (process.HasExited) return;
        process.Refresh();
        if (handle == 0) handle = process.MainWindowHandle;
        if (handle != 0) Assert.True(NativeMethods.PostMessage(handle, 0x10, 0, 0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process.WaitForExitAsync(timeout.Token);
    }
    private sealed class Sink : IDiagnosticSink
    {
        internal ConcurrentQueue<DiagnosticEntry> Entries { get; } = new();
        public bool IsHealthy => true;
        public void Record(DiagnosticEntry entry) => Entries.Enqueue(entry);
    }
}
