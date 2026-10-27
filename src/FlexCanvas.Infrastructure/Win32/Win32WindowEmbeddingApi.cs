using System.ComponentModel;
using System.Runtime.InteropServices;
using FlexCanvas.Application;
using FlexCanvas.Domain;

namespace FlexCanvas.Infrastructure.Win32;

/// <summary>Production native adapter. No prototype whitelist; strict gates precede mutation.</summary>
public sealed class Win32WindowEmbeddingApi : IWindowEmbeddingApi
{
    public NativeWindowResult Capture(WindowIdentity target, nint host, out WindowSnapshot? snapshot)
    {
        snapshot = null;
        if (!Environment.Is64BitProcess || target.ProcessId <= 4 || target.ProcessId == (uint)Environment.ProcessId)
            return NativeWindowResult.Fail(OperationCodes.WindowNotSupported);
        if (!Matches(target) || !NativeMethods.IsWindow(host)) return NativeWindowResult.Fail(OperationCodes.WindowNotFound);
        if (NativeMethods.GetWindowThreadProcessId(host, out uint ownPid) == 0 || ownPid != (uint)Environment.ProcessId)
            return NativeWindowResult.Fail(OperationCodes.InvalidArgument);
        if (!NativeMethods.IsWindowVisible(target.Hwnd) || NativeMethods.IsIconic(target.Hwnd))
            return NativeWindowResult.Fail(OperationCodes.WindowNotVisible);
        var read = ReadStyle(target.Hwnd, -16, out long style);
        if (!read.Success) return read;
        read = ReadStyle(target.Hwnd, -20, out long extended);
        if (!read.Success) return read;
        if ((style & 0x40000000L) != 0 || (extended & (0x80L | 0x80000L | 0x08000000L)) != 0
            || NativeMethods.GetWindow(target.Hwnd, 4) != 0) return NativeWindowResult.Fail(OperationCodes.WindowNotSupported);
        int hr = NativeMethods.DwmGetWindowAttribute(target.Hwnd, 14, out int cloaked, sizeof(int));
        if (hr != 0 || cloaked != 0) return NativeWindowResult.Fail(OperationCodes.WindowNotVisible, NativeApi.DwmGetWindowAttribute, hr);
        read = ReadSecurity(target.ProcessId, out var security);
        if (!read.Success) return read;
        read = ReadSecurity((uint)Environment.ProcessId, out var ownSecurity);
        if (!read.Success) return read;
        if (security != ownSecurity || security.UiAccess || ownSecurity.UiAccess)
            return NativeWindowResult.Fail(OperationCodes.AccessDenied);
        nint context = NativeMethods.GetWindowDpiAwarenessContext(target.Hwnd);
        nint hostContext = NativeMethods.GetWindowDpiAwarenessContext(host);
        if (context == 0 || hostContext == 0 || !NativeMethods.AreDpiAwarenessContextsEqual(context, -4)
            || !NativeMethods.AreDpiAwarenessContextsEqual(context, hostContext))
            return NativeWindowResult.Fail(OperationCodes.DpiMismatch);
        if (!NativeMethods.GetWindowRect(target.Hwnd, out var rect)) return Error(NativeApi.GetWindowRect);
        var placement = new NativeMethods.Placement { Length = (uint)Marshal.SizeOf<NativeMethods.Placement>() };
        if (!NativeMethods.GetWindowPlacement(target.Hwnd, ref placement)) return Error(NativeApi.GetWindowPlacement);
        using var process = NativeMethods.OpenProcess(0x1000, false, target.ProcessId);
        if (process.IsInvalid) return Error(NativeApi.OpenProcess, OperationCodes.AccessDenied);
        if (!NativeMethods.GetProcessTimes(process, out long creation, out _, out _, out _)) return Error(NativeApi.GetProcessTimes);
        if (!Matches(target)) return NativeWindowResult.Fail(OperationCodes.WindowNotFound);
        uint dpi = NativeMethods.GetDpiForWindow(target.Hwnd);
        if (dpi == 0) return NativeWindowResult.Fail(OperationCodes.DpiMismatch);
        snapshot = new(target, creation, NativeMethods.GetParent(target.Hwnd), style, extended, Bounds(rect),
            new(placement.Flags, placement.ShowCommand, placement.MinPosition.X, placement.MinPosition.Y,
                placement.MaxPosition.X, placement.MaxPosition.Y, Bounds(placement.NormalPosition)), dpi);
        return NativeWindowResult.Ok();
    }

    public bool IsAlive(WindowSnapshot snapshot)
    {
        if (!Matches(snapshot.Identity)) return false;
        using var process = NativeMethods.OpenProcess(0x1000, false, snapshot.Identity.ProcessId);
        if (process.IsInvalid) throw new Win32Exception(Marshal.GetLastPInvokeError(), "OpenProcess failed during identity check.");
        if (!NativeMethods.GetProcessTimes(process, out long creation, out _, out _, out _))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "GetProcessTimes failed during identity check.");
        return creation == snapshot.ProcessCreationTime && Matches(snapshot.Identity);
    }
    public NativeWindowResult SetStyles(nint hwnd, long style, long extendedStyle)
    {
        var result = WriteStyle(hwnd, -16, style);
        if (result.Success) result = WriteStyle(hwnd, -20, extendedStyle);
        if (!result.Success) return result;
        return NativeMethods.SetWindowPos(hwnd, 0, 0, 0, 0, 0, 0x0037)
            ? NativeWindowResult.Ok() : Error(NativeApi.SetWindowPos);
    }
    public NativeWindowResult SetParent(nint hwnd, nint parent)
    {
        NativeMethods.SetLastError(0);
        nint previous = NativeMethods.SetParent(hwnd, parent);
        int error = Marshal.GetLastPInvokeError();
        if (previous == 0 && error != 0) return NativeWindowResult.Fail(OperationCodes.EmbeddingFailed, NativeApi.SetParent, error);
        // With WS_CHILD still set, SetParent(NULL) temporarily reports the desktop as parent.
        // Final top-level parent verification runs after restoring the original styles.
        return parent == 0 || NativeMethods.GetParent(hwnd) == parent ? NativeWindowResult.Ok()
            : NativeWindowResult.Fail(OperationCodes.EmbeddingFailed, NativeApi.SetParent);
    }
    public NativeWindowResult GetClientBounds(nint host, out WindowBounds bounds)
    {
        bounds = default;
        if (!NativeMethods.GetClientRect(host, out var rect)) return Error(NativeApi.GetClientRect);
        bounds = new(0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return bounds.Width > 0 && bounds.Height > 0 ? NativeWindowResult.Ok() : NativeWindowResult.Fail(OperationCodes.InvalidArgument);
    }
    public NativeWindowResult SetBounds(nint hwnd, WindowBounds bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return NativeWindowResult.Fail(OperationCodes.InvalidArgument);
        // Avoid self-generated location event loops while retaining FRAMECHANGED for style updates.
        if ((NativeMethods.GetWindowLongPtr(hwnd, -16).ToInt64() & 0x40000000L) != 0
            && NativeMethods.GetWindowRect(hwnd, out var current) && NativeMethods.GetParent(hwnd) is var parent && parent != 0)
        {
            var origin = new NativeMethods.Point();
            if (NativeMethods.ClientToScreen(parent, ref origin) && current.Left == origin.X + bounds.X
                && current.Top == origin.Y + bounds.Y && current.Right - current.Left == bounds.Width
                && current.Bottom - current.Top == bounds.Height) return NativeWindowResult.Ok();
        }
        if (!NativeMethods.SetWindowPos(hwnd, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0034))
            return Error(NativeApi.SetWindowPos);
        return NativeWindowResult.Ok();
    }
    public NativeWindowResult RestorePlacement(nint hwnd, WindowPlacement placement)
    {
        var value = new NativeMethods.Placement
        {
            Length = (uint)Marshal.SizeOf<NativeMethods.Placement>(), Flags = placement.Flags,
            ShowCommand = placement.ShowCommand, MinPosition = new() { X = placement.MinX, Y = placement.MinY },
            MaxPosition = new() { X = placement.MaxX, Y = placement.MaxY },
            NormalPosition = Rect(placement.NormalBounds)
        };
        return NativeMethods.SetWindowPlacement(hwnd, value) ? NativeWindowResult.Ok() : Error(NativeApi.SetWindowPlacement);
    }
    public NativeWindowResult VerifyRestored(WindowSnapshot snapshot)
    {
        var result = ReadStyle(snapshot.Identity.Hwnd, -16, out long style);
        if (!result.Success) return result;
        result = ReadStyle(snapshot.Identity.Hwnd, -20, out long extended);
        if (!result.Success) return result;
        if (!NativeMethods.GetWindowRect(snapshot.Identity.Hwnd, out var rect)) return Error(NativeApi.GetWindowRect);
        if (NativeMethods.GetParent(snapshot.Identity.Hwnd) != snapshot.Parent) return NativeWindowResult.Fail(OperationCodes.DetachFailed, NativeApi.SetParent);
        if (style != snapshot.Style || extended != snapshot.ExtendedStyle) return NativeWindowResult.Fail(OperationCodes.DetachFailed, NativeApi.SetWindowLongPtr);
        if (!NativeMethods.IsWindowVisible(snapshot.Identity.Hwnd)) return NativeWindowResult.Fail(OperationCodes.DetachFailed, NativeApi.SetWindowPlacement);
        return snapshot.Placement.ShowCommand == 3 || Bounds(rect) == snapshot.Bounds
            ? NativeWindowResult.Ok() : NativeWindowResult.Fail(OperationCodes.DetachFailed, NativeApi.SetWindowPos);
    }
    public NativeWindowResult GetTargetBounds(nint hwnd, nint host, out WindowBounds bounds)
    {
        bounds = default;
        if (!NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindow(host))
            return NativeWindowResult.Fail(OperationCodes.WindowNotFound);
        if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return Error(NativeApi.GetWindowRect);
        var origin = new NativeMethods.Point { X = rect.Left, Y = rect.Top };
        if (!NativeMethods.ScreenToClient(host, ref origin)) return Error(NativeApi.SetWindowPos, OperationCodes.InternalError);
        bounds = new(origin.X, origin.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return bounds.Width > 0 && bounds.Height > 0
            ? NativeWindowResult.Ok() : NativeWindowResult.Fail(OperationCodes.InvalidArgument);
    }

    public NativeWindowResult Focus(nint hwnd)
    {
        uint targetThread = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        uint ownThread = NativeMethods.GetCurrentThreadId();
        if (targetThread == 0) return Error(NativeApi.GetWindowThreadProcessId);
        bool attached = ownThread != targetThread;
        if (attached && !NativeMethods.AttachThreadInput(ownThread, targetThread, true)) return Error(NativeApi.AttachThreadInput);
        NativeWindowResult result;
        NativeWindowResult detach = NativeWindowResult.Ok();
        try
        {
            NativeMethods.SetLastError(0);
            NativeMethods.SetFocus(hwnd);
            var info = new NativeMethods.GuiThreadInfo { Size = (uint)Marshal.SizeOf<NativeMethods.GuiThreadInfo>() };
            result = NativeMethods.GetGUIThreadInfo(targetThread, ref info) && info.Focus == hwnd
                ? NativeWindowResult.Ok() : Error(NativeApi.GetGuiThreadInfo);
        }
        finally
        {
            if (attached && !NativeMethods.AttachThreadInput(ownThread, targetThread, false)) detach = Error(NativeApi.AttachThreadInput);
        }
        return detach.Success ? result : detach;
    }
    private static bool Matches(WindowIdentity target) => NativeMethods.IsWindow(target.Hwnd)
        && NativeMethods.GetWindowThreadProcessId(target.Hwnd, out uint pid) != 0 && pid == target.ProcessId;
    private static WindowBounds Bounds(NativeMethods.Rect rect) => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    private static NativeMethods.Rect Rect(WindowBounds b) => new() { Left = b.X, Top = b.Y, Right = b.X + b.Width, Bottom = b.Y + b.Height };
    private static NativeWindowResult ReadStyle(nint hwnd, int index, out long value)
    {
        NativeMethods.SetLastError(0);
        nint result = NativeMethods.GetWindowLongPtr(hwnd, index);
        int error = Marshal.GetLastPInvokeError();
        value = result.ToInt64() & uint.MaxValue;
        return result == 0 && error != 0 ? NativeWindowResult.Fail(OperationCodes.EmbeddingFailed, NativeApi.GetWindowLongPtr, error) : NativeWindowResult.Ok();
    }
    private static NativeWindowResult WriteStyle(nint hwnd, int index, long value)
    {
        NativeMethods.SetLastError(0);
        nint previous = NativeMethods.SetWindowLongPtr(hwnd, index, (nint)value);
        int error = Marshal.GetLastPInvokeError();
        return previous == 0 && error != 0 ? NativeWindowResult.Fail(OperationCodes.EmbeddingFailed, NativeApi.SetWindowLongPtr, error) : NativeWindowResult.Ok();
    }
    private static NativeWindowResult ReadSecurity(uint pid, out (bool Elevated, int Integrity, bool UiAccess) security)
    {
        security = default;
        using var process = NativeMethods.OpenProcess(0x1000, false, pid);
        if (process.IsInvalid) return Error(NativeApi.OpenProcess, OperationCodes.AccessDenied);
        if (!NativeMethods.OpenProcessToken(process, 8, out var token)) return Error(NativeApi.OpenProcessToken, OperationCodes.AccessDenied);
        using (token)
        {
            if (!NativeMethods.GetTokenInformation(token, 20, out int elevated, sizeof(int), out _)
                || !NativeMethods.GetTokenInformation(token, 26, out int uiAccess, sizeof(int), out _))
                return Error(NativeApi.GetTokenInformation, OperationCodes.AccessDenied);
            NativeMethods.GetTokenInformationBuffer(token, 25, 0, 0, out int length);
            int sizingError = Marshal.GetLastPInvokeError();
            if (length <= 0 || length > 65536) return NativeWindowResult.Fail(OperationCodes.AccessDenied, NativeApi.GetTokenInformation, sizingError);
            nint buffer = Marshal.AllocHGlobal(length);
            try
            {
                if (!NativeMethods.GetTokenInformationBuffer(token, 25, buffer, length, out _)) return Error(NativeApi.GetTokenInformation, OperationCodes.AccessDenied);
                nint sid = Marshal.ReadIntPtr(buffer);
                int count = Marshal.ReadByte(NativeMethods.GetSidSubAuthorityCount(sid));
                if (count == 0) return NativeWindowResult.Fail(OperationCodes.AccessDenied);
                int integrity = Marshal.ReadInt32(NativeMethods.GetSidSubAuthority(sid, (uint)(count - 1)));
                security = (elevated != 0, integrity, uiAccess != 0);
                return NativeWindowResult.Ok();
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
    }
    private static NativeWindowResult Error(NativeApi api, string code = OperationCodes.EmbeddingFailed) =>
        NativeWindowResult.Fail(code, api, Marshal.GetLastPInvokeError());
}
