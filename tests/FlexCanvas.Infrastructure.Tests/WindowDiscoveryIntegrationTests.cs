using System.Diagnostics;
using System.IO;
using FlexCanvas.Application;
using FlexCanvas.Domain;
using FlexCanvas.Infrastructure.Win32;
using Xunit;

namespace FlexCanvas.Infrastructure.Tests;

[Collection("Native probes")]
public sealed class WindowDiscoveryIntegrationTests
{
    [Fact]
    [Trait("Category", "NativeIntegration")]
    public async Task DiscoveryFiltersControlledWindowsAndRechecksIdentityWithoutMutation()
    {
        string root = FindRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string exe = Path.Combine(root, "tools", "Probes", "Win32Probe", "bin", configuration,
            "net8.0-windows", "FlexCanvas.Win32Probe.exe");
        using var first = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false })!;
        using var second = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false })!;
        await using var dispatcher = new WindowHostDispatcher();
        var service = new Win32WindowDiscoveryService(dispatcher, new Sink());
        nint a = 0, b = 0, own = 0;
        try
        {
            a = await WaitForWindow(first);
            b = await WaitForWindow(second);
            var expected = await dispatcher.InvokeAsync(() =>
            {
                own = NativeMethods.CreateWindowEx(0, "STATIC", "Own discovery host", 0x10000000, 0, 0, 200, 200, 0, 0, 0, 0);
                Assert.NotEqual((nint)0, own);
                Assert.True(NativeMethods.SetWindowText(a, "Duplicate discovery title"));
                Assert.True(NativeMethods.SetWindowText(b, "Duplicate discovery title"));
                Assert.True(NativeMethods.GetWindowRect(a, out var rect));
                Assert.True(Win32ProbeWindowApi.ReadStyle(a, -16, out long style).Success);
                Assert.True(Win32ProbeWindowApi.ReadStyle(a, -20, out long ex).Success);
                return (Rect: rect, Style: style, Ex: ex, Parent: NativeMethods.GetParent(a));
            });
            var result = await service.EnumerateAsync();
            Assert.True(result.Success);
            var candidates = result.Data!;
            Assert.DoesNotContain(candidates, w => w.Identity.ProcessId == (uint)Environment.ProcessId);
            var wa = Assert.Single(candidates, w => w.Identity.Hwnd == a);
            var wb = Assert.Single(candidates, w => w.Identity.Hwnd == b);
            Assert.Equal(wa.Title, wb.Title);
            Assert.NotEqual(wa.Identity, wb.Identity);
            Assert.Equal((uint)first.Id, wa.Identity.ProcessId);
            Assert.Equal("FlexCanvas.Win32Probe", wa.ProcessName);
            Assert.NotEmpty(wa.ClassName);
            Assert.Equal(new WindowBounds(expected.Rect.Left, expected.Rect.Top,
                expected.Rect.Right - expected.Rect.Left, expected.Rect.Bottom - expected.Rect.Top), wa.Bounds);
            Assert.Equal(expected.Style, wa.Style);
            Assert.Equal(expected.Ex, wa.ExtendedStyle);
            Assert.NotNull(wa.IsElevated);
            Assert.Equal(DpiMode.PerMonitorV2, wa.DpiMode);
            Assert.True(wa.Dpi > 0);
            await dispatcher.InvokeAsync(() =>
            {
                Assert.Equal(expected.Parent, NativeMethods.GetParent(a));
                Assert.True(NativeMethods.GetWindowRect(a, out var after));
                Assert.Equal(expected.Rect, after);
                return true;
            });
            Assert.Equal(OperationCodes.WindowNotFound,
                (await service.ReadAsync(new(a, (uint)second.Id))).Code);
            Assert.Equal(OperationCodes.InvalidArgument, (await service.ReadAsync(default)).Code);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Equal(OperationCodes.Cancelled, (await service.EnumerateAsync(cancelled.Token)).Code);

            await dispatcher.InvokeAsync(() => NativeMethods.SetWindowText(a, ""));
            Assert.DoesNotContain((await service.EnumerateAsync()).Data!, w => w.Identity.Hwnd == a);
            await dispatcher.InvokeAsync(() =>
            {
                Assert.True(NativeMethods.SetWindowText(a, "Duplicate discovery title"));
                var api = new Win32ProbeWindowApi(Path.Combine(root, "tools"));
                Assert.True(api.SetStyles(a, expected.Style, expected.Ex | 0x80).Success);
                return true;
            });
            Assert.DoesNotContain((await service.EnumerateAsync()).Data!, w => w.Identity.Hwnd == a);
            await dispatcher.InvokeAsync(() =>
            {
                Assert.True(new Win32ProbeWindowApi(Path.Combine(root, "tools")).SetStyles(a, expected.Style, expected.Ex).Success);
                NativeMethods.ShowWindow(a, 0);
                return true;
            });
            Assert.DoesNotContain((await service.EnumerateAsync()).Data!, w => w.Identity.Hwnd == a);
            await dispatcher.InvokeAsync(() => NativeMethods.ShowWindow(a, 5));
            Assert.True(NativeMethods.PostMessage(a, 0x10, 0, 0));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await first.WaitForExitAsync(timeout.Token);
            Assert.Equal(OperationCodes.WindowNotFound, (await service.ReadAsync(wa.Identity)).Code);
        }
        finally
        {
            foreach (var (process, handle) in new[] { (first, a), (second, b) })
            {
                if (process.HasExited) continue;
                process.Refresh();
                nint closeHandle = handle != 0 ? handle : process.MainWindowHandle;
                if (closeHandle != 0) Assert.True(NativeMethods.PostMessage(closeHandle, 0x10, 0, 0));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(timeout.Token);
            }
            if (own != 0) await dispatcher.InvokeAsync(() => NativeMethods.DestroyWindow(own));
        }
    }

    private static async Task<nint> WaitForWindow(Process process)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            process.Refresh();
            if (process.HasExited) throw new InvalidOperationException("Test probe exited.");
            if (process.MainWindowHandle != 0) return process.MainWindowHandle;
            await Task.Delay(100);
        }
        throw new TimeoutException("Test probe did not show.");
    }
    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (File.Exists(Path.Combine(folder.FullName, "FlexCanvas.sln"))) return folder.FullName;
        throw new InvalidOperationException("Checkout not found.");
    }
    private sealed class Sink : IDiagnosticSink
    {
        public bool IsHealthy => true;
        public void Record(DiagnosticEntry entry) { }
    }
}
