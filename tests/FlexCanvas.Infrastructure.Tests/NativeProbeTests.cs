using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FlexCanvas.Domain.Poc;
using FlexCanvas.Infrastructure.Win32;
using Xunit;
using Xunit.Abstractions;

namespace FlexCanvas.Infrastructure.Tests;

[CollectionDefinition("Native probes", DisableParallelization = true)]
public sealed class NativeProbeCollection
{
}

[Collection("Native probes")]
public sealed class NativeProbeTests(ITestOutputHelper output)
{
    [Theory]
    [Trait("Category", "NativeIntegration")]
    [InlineData("Win32")]
    [InlineData("Wpf")]
    [InlineData("WinForms")]
    public async Task ControlledProbeEmbedsMovesResizesAndRestores(string kind)
    {
        string root = FindRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string exe = Path.Combine(root, "tools", "Probes", $"{kind}Probe", "bin", configuration,
            "net8.0-windows", $"FlexCanvas.{kind}Probe.exe");
        Assert.True(File.Exists(exe), "Build all probe project dependencies first.");
        using var process = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false })!;
        await using var dispatcher = new WindowHostDispatcher();
        var api = new Win32ProbeWindowApi(Path.Combine(root, "tools"));
        var engine = new ProbeEmbeddingEngine(api);
        nint host = 0, target = 0;
        try
        {
            target = await WaitForWindowAsync(process);
            host = await dispatcher.InvokeAsync(() =>
            {
                Assert.NotEqual((nint)0, NativeMethods.SetThreadDpiAwarenessContext(-4));
                nint handle = NativeMethods.CreateWindowEx(0, "STATIC", "FlexCanvas POC test host",
                    0x10000000, 150, 150, 800, 600, 0, 0, 0, 0);
                Assert.NotEqual((nint)0, handle);
                return handle;
            });
            await dispatcher.InvokeAsync(() =>
            {
                Assert.True(api.Capture(target, host, out var original).Success);
                nint emptyHost = NativeMethods.CreateWindowEx(0, "STATIC", "Empty POC client",
                    0x80000000, 0, 0, 0, 0, 0, 0, 0, 0);
                Assert.NotEqual((nint)0, emptyHost);
                try
                {
                    var failed = engine.Embed(target, emptyHost);
                    Assert.False(failed.Success);
                    Assert.Contains("Rollback: restored", failed.Message);
                    Assert.Equal(original!.Parent, NativeMethods.GetParent(target));
                    Assert.True(Win32ProbeWindowApi.ReadStyle(target, -16, out long style).Success);
                    Assert.True(Win32ProbeWindowApi.ReadStyle(target, -20, out long extended).Success);
                    Assert.Equal(original.Style, style);
                    Assert.Equal(original.ExtendedStyle, extended);
                    Assert.True(NativeMethods.GetWindowRect(target, out var restoredRect));
                    Assert.Equal(original.Bounds, new ProbeBounds(restoredRect.Left, restoredRect.Top,
                        restoredRect.Right - restoredRect.Left, restoredRect.Bottom - restoredRect.Top));
                    output.WriteLine($"{kind} real empty-client failure rollback: {failed.Code}; original state restored.");
                }
                finally { Assert.True(NativeMethods.DestroyWindow(emptyHost)); }
                return true;
            });
            for (int cycle = 0; cycle < 3; cycle++)
            {
                var embedded = await dispatcher.InvokeAsync(() => engine.Embed(target, host));
                Assert.True(embedded.Success, $"{kind}: {embedded.Code} {embedded.Message} error={embedded.Win32Error}");
                var original = engine.Status!.Original;
                output.WriteLine($"{kind} cycle={cycle} pid={original.ProcessId} dpi={original.Dpi} original={original.Bounds}");
                await dispatcher.InvokeAsync(() =>
                {
                    Assert.Equal(host, NativeMethods.GetParent(target));
                    Assert.True(Win32ProbeWindowApi.ReadStyle(target, -16, out long style).Success);
                    Assert.NotEqual(0, style & 0x40000000L);
                    Assert.True(api.SetBounds(host, new(220 + cycle * 10, 180, 900, 650)).Success);
                    Assert.True(engine.Resize().Success);
                    Assert.True(NativeMethods.GetClientRect(host, out var hostRect));
                    var origin = new NativeMethods.Point();
                    Assert.True(NativeMethods.ClientToScreen(host, ref origin));
                    Assert.True(NativeMethods.GetWindowRect(target, out var targetRect));
                    Assert.Equal(origin.X, targetRect.Left);
                    Assert.Equal(origin.Y, targetRect.Top);
                    Assert.Equal(hostRect.Right - hostRect.Left, targetRect.Right - targetRect.Left);
                    Assert.Equal(hostRect.Bottom - hostRect.Top, targetRect.Bottom - targetRect.Top);
                    return true;
                });
                var detached = await dispatcher.InvokeAsync(engine.Detach);
                Assert.True(detached.Success, detached.Message);
                Assert.True(await dispatcher.InvokeAsync(() =>
                {
                    Assert.Equal(original.Parent, NativeMethods.GetParent(target));
                    Assert.True(Win32ProbeWindowApi.ReadStyle(target, -16, out long style).Success);
                    Assert.True(Win32ProbeWindowApi.ReadStyle(target, -20, out long extended).Success);
                    Assert.Equal(original.Style, style);
                    Assert.Equal(original.ExtendedStyle, extended);
                    Assert.True(NativeMethods.GetWindowRect(target, out var rect));
                    Assert.Equal(original.Bounds, new ProbeBounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top));
                    return true;
                }));
                Assert.False(process.HasExited);
            }
            Assert.True((await dispatcher.InvokeAsync(() => engine.Embed(target, host))).Success);
            Assert.True(NativeMethods.PostMessage(target, 0x0010, 0, 0));
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(EmbeddingState.TargetExited, (await dispatcher.InvokeAsync(engine.Poll))!.State);
            Assert.True((await dispatcher.InvokeAsync(engine.Detach)).Success);
            output.WriteLine($"{kind} normal target exit while embedded: TargetExited, idempotent detach.");
        }
        finally
        {
            var restored = await dispatcher.InvokeAsync(engine.Detach);
            output.WriteLine($"Cleanup: {restored.Code} {restored.Message}");
            if (!process.HasExited)
            {
                if (target == 0) { process.Refresh(); target = process.MainWindowHandle; }
                if (target != 0) Assert.True(NativeMethods.PostMessage(target, 0x0010, 0, 0));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(timeout.Token);
            }
            if (host != 0) Assert.True(await dispatcher.InvokeAsync(() => NativeMethods.DestroyWindow(host)));
        }
    }

    [Fact]
    [Trait("Category", "NativeIntegration")]
    public async Task InvalidAndOwnWindowsAreRejectedBeforeMutation()
    {
        await using var dispatcher = new WindowHostDispatcher();
        await dispatcher.InvokeAsync(() =>
        {
            nint host = NativeMethods.CreateWindowEx(0, "STATIC", "Controlled own host", 0, 0, 0, 100, 100, 0, 0, 0, 0);
            Assert.NotEqual((nint)0, host);
            try
            {
                var api = new Win32ProbeWindowApi(Path.Combine(FindRoot(), "tools"));
                Assert.Equal("WINDOW_NOT_FOUND", api.Capture(0, host, out _).Code);
                Assert.Equal("WINDOW_NOT_SUPPORTED", api.Capture(host, host, out _).Code);
                Assert.True(NativeMethods.GetWindowRect(host, out var before));
                Assert.Equal("INVALID_ARGUMENT", api.SetBounds(host, new(0, 0, 0, 100)).Code);
                Assert.True(NativeMethods.GetWindowRect(host, out var rect));
                Assert.Equal(before.Left, rect.Left);
                Assert.Equal(before.Top, rect.Top);
                Assert.Equal(before.Right, rect.Right);
                Assert.Equal(before.Bottom, rect.Bottom);
            }
            finally { Assert.True(NativeMethods.DestroyWindow(host)); }
            return true;
        });
    }

    private static async Task<nint> WaitForWindowAsync(Process process)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            process.Refresh();
            if (process.HasExited) throw new InvalidOperationException("Controlled probe exited before showing a window.");
            if (process.MainWindowHandle != 0) return process.MainWindowHandle;
            await Task.Delay(100);
        }
        throw new TimeoutException("Controlled probe did not show a window within 10 seconds.");
    }

    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (File.Exists(Path.Combine(folder.FullName, "FlexCanvas.sln"))) return folder.FullName;
        throw new InvalidOperationException("Test source checkout was not found.");
    }
}
