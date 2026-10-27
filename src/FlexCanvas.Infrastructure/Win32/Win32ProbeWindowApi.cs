using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FlexCanvas.Application.Poc;
using FlexCanvas.Domain.Poc;

namespace FlexCanvas.Infrastructure.Win32;

/// <summary>Native adapter restricted to repository-built, same-privilege probe processes.</summary>
public sealed class Win32ProbeWindowApi(string probeRoot) : IProbeWindowApi
{
    private readonly string _probeRoot = Path.GetFullPath(probeRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    private static readonly string[] ProbeNames = ["FlexCanvas.WpfProbe", "FlexCanvas.WinFormsProbe", "FlexCanvas.Win32Probe"];

    /// <summary>Enumerates only controlled probes; never logs their text input or document contents.</summary>
    public IReadOnlyList<ProbeCandidate> Discover()
    {
        var candidates = new List<ProbeCandidate>();
        NativeMethods.EnumWindowProc callback = (hwnd, _) =>
        {
            if (NativeMethods.IsWindowVisible(hwnd) && NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid) != 0
                && TryGetKind(pid, out string? kind))
                candidates.Add(new(hwnd, pid, kind!));
            return true;
        };
        if (!NativeMethods.EnumWindows(callback, 0))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "EnumWindows failed.");
        GC.KeepAlive(callback);
        return candidates;
    }

    /// <inheritdoc />
    public bool IsAlive(nint hwnd, uint processId) =>
        NativeMethods.IsWindow(hwnd) && NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid) != 0 && pid == processId;

    /// <inheritdoc />
    public ProbeResult Capture(nint target, nint host, out ProbeSnapshot? snapshot)
    {
        snapshot = null;
        if (!Environment.Is64BitProcess) return ProbeResult.Fail("WINDOW_NOT_SUPPORTED", "This POC requires x64.");
        if (!NativeMethods.IsWindow(target) || !NativeMethods.IsWindow(host))
            return ProbeResult.Fail("WINDOW_NOT_FOUND", "Target or host HWND no longer exists.");
        if (NativeMethods.GetWindowThreadProcessId(target, out uint pid) == 0)
            return Error("GetWindowThreadProcessId");
        if (pid == (uint)Environment.ProcessId || !TryGetKind(pid, out _))
            return ProbeResult.Fail("WINDOW_NOT_SUPPORTED", "Only separate repository-built probes are allowed.");
        if (NativeMethods.GetWindowThreadProcessId(host, out uint hostPid) == 0 || hostPid != (uint)Environment.ProcessId)
            return ProbeResult.Fail("INVALID_ARGUMENT", "Host HWND must belong to this process.");
        if (!NativeMethods.IsWindowVisible(target) || NativeMethods.IsIconic(target))
            return ProbeResult.Fail("WINDOW_NOT_VISIBLE", "Restore/show the probe before embedding.");
        var security = ReadElevation(pid, out bool elevation);
        if (!security.Success) return security;
        security = ReadElevation((uint)Environment.ProcessId, out bool ownElevation);
        if (!security.Success) return security;
        if (elevation != ownElevation)
            return ProbeResult.Fail("ACCESS_DENIED", "Mismatched elevation; no automatic elevation.");
        nint targetContext = NativeMethods.GetWindowDpiAwarenessContext(target);
        nint hostContext = NativeMethods.GetWindowDpiAwarenessContext(host);
        if (targetContext == 0 || hostContext == 0 || !NativeMethods.AreDpiAwarenessContextsEqual(targetContext, hostContext))
            return ProbeResult.Fail("DPI_MISMATCH", "This initial POC requires equal DPI awareness modes.");
        var result = ReadStyle(target, -16, out long style);
        if (!result.Success) return result;
        result = ReadStyle(target, -20, out long extended);
        if (!result.Success) return result;
        if ((style & 0x40000000L) != 0 || NativeMethods.GetWindow(target, 4) != 0)
            return ProbeResult.Fail("WINDOW_NOT_SUPPORTED", "This POC only embeds unowned top-level probe windows.");
        if (!NativeMethods.GetWindowRect(target, out var rect)) return Error("GetWindowRect");
        snapshot = new(target, pid, NativeMethods.GetParent(target), style, extended,
            new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), NativeMethods.GetDpiForWindow(target));
        return ProbeResult.Ok();
    }

    /// <inheritdoc />
    public ProbeResult SetStyles(nint hwnd, long style, long extendedStyle)
    {
        var result = WriteStyle(hwnd, -16, style);
        return result.Success ? WriteStyle(hwnd, -20, extendedStyle) : result;
    }

    /// <inheritdoc />
    public ProbeResult SetParent(nint hwnd, nint parent)
    {
        NativeMethods.SetLastError(0);
        nint previous = NativeMethods.SetParent(hwnd, parent);
        int error = Marshal.GetLastPInvokeError();
        return previous == 0 && error != 0 ? ProbeResult.Fail("EMBEDDING_FAILED", "SetParent failed.", error) : ProbeResult.Ok();
    }

    /// <inheritdoc />
    public ProbeResult SetBounds(nint hwnd, ProbeBounds bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return ProbeResult.Fail("INVALID_ARGUMENT", "Dimensions must be positive physical pixels.");
        // FRAMECHANGED refreshes cached styles; NOACTIVATE avoids stealing focus.
        return NativeMethods.SetWindowPos(hwnd, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0034)
            ? ProbeResult.Ok() : Error("SetWindowPos");
    }

    /// <inheritdoc />
    public ProbeResult GetClientBounds(nint hwnd, out ProbeBounds bounds)
    {
        bounds = default;
        if (!NativeMethods.GetClientRect(hwnd, out var rect)) return Error("GetClientRect");
        bounds = new(0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return bounds.Width > 0 && bounds.Height > 0 ? ProbeResult.Ok()
            : ProbeResult.Fail("INVALID_ARGUMENT", "Host client area is empty.");
    }

    internal static ProbeResult ReadStyle(nint hwnd, int index, out long value)
    {
        NativeMethods.SetLastError(0);
        nint result = NativeMethods.GetWindowLongPtr(hwnd, index);
        int error = Marshal.GetLastPInvokeError();
        value = result.ToInt64() & uint.MaxValue;
        return result == 0 && error != 0 ? ProbeResult.Fail("EMBEDDING_FAILED", "GetWindowLongPtrW failed.", error) : ProbeResult.Ok();
    }

    private static ProbeResult WriteStyle(nint hwnd, int index, long value)
    {
        NativeMethods.SetLastError(0);
        nint previous = NativeMethods.SetWindowLongPtr(hwnd, index, (nint)value);
        int error = Marshal.GetLastPInvokeError();
        return previous == 0 && error != 0 ? ProbeResult.Fail("EMBEDDING_FAILED", "SetWindowLongPtrW failed.", error) : ProbeResult.Ok();
    }

    private bool TryGetKind(uint pid, out string? kind)
    {
        kind = null;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            if (!ProbeNames.Contains(process.ProcessName, StringComparer.Ordinal)) return false;
            string? path = process.MainModule?.FileName;
            if (path is null || !Path.GetFullPath(path).StartsWith(_probeRoot, StringComparison.OrdinalIgnoreCase)) return false;
            kind = process.ProcessName;
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or Win32Exception or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static ProbeResult ReadElevation(uint pid, out bool elevated)
    {
        elevated = false;
        using var process = NativeMethods.OpenProcess(0x1000, false, pid);
        if (process.IsInvalid) return Error("OpenProcess") with { Code = "ACCESS_DENIED" };
        if (!NativeMethods.OpenProcessToken(process, 0x0008, out var token))
            return Error("OpenProcessToken") with { Code = "ACCESS_DENIED" };
        using (token)
        {
            if (!NativeMethods.GetTokenInformation(token, 20, out int elevation, sizeof(int), out _))
                return Error("GetTokenInformation") with { Code = "ACCESS_DENIED" };
            elevated = elevation != 0;
            return ProbeResult.Ok();
        }
    }

    private static ProbeResult Error(string api)
    {
        int error = Marshal.GetLastPInvokeError();
        return ProbeResult.Fail("EMBEDDING_FAILED", $"{api} failed.", error);
    }
}
