using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FlexCanvas.Application;
using FlexCanvas.Domain;

namespace FlexCanvas.Infrastructure.Win32;

public sealed class Win32WindowDiscoveryService(WindowHostDispatcher dispatcher, IDiagnosticSink diagnostics) : IWindowDiscoveryService
{
    public Task<OperationResult<IReadOnlyList<ExternalWindowInfo>>> EnumerateAsync(CancellationToken cancellationToken = default) =>
        RunAsync<IReadOnlyList<ExternalWindowInfo>>((id, token) =>
        {
            var windows = new List<ExternalWindowInfo>();
            var processes = new Dictionary<uint, (string Name, bool? Elevated)>();
            bool? ownElevation = ReadElevation((uint)Environment.ProcessId, id);
            Exception? callbackError = null;
            NativeMethods.EnumWindowProc callback = (hwnd, parameter) =>
            {
                if (token.IsCancellationRequested) return false;
                try
                {
                    var window = ReadCandidate(hwnd, null, processes, ownElevation, id, out _);
                    if (window is not null) windows.Add(window);
                    return true;
                }
                catch (Exception error)
                {
                    // Never allow a managed exception to unwind through EnumWindows.
                    callbackError = error;
                    return false;
                }
            };
            NativeMethods.SetLastError(0);
            bool success = NativeMethods.EnumWindows(callback, 0);
            int errorCode = Marshal.GetLastPInvokeError();
            GC.KeepAlive(callback);
            token.ThrowIfCancellationRequested();
            if (callbackError is not null) throw callbackError;
            if (!success)
            {
                LogNative(id, NativeApi.EnumWindows, errorCode);
                return OperationResult<IReadOnlyList<ExternalWindowInfo>>.Fail(OperationCodes.InternalError, "Enumeration failed.", id.ToString("N"));
            }
            diagnostics.Record(new(DiagnosticEvent.Discovery, DiagnosticLevel.Debug, id));
            return OperationResult<IReadOnlyList<ExternalWindowInfo>>.Ok(windows, id.ToString("N"));
        }, cancellationToken);

    public Task<OperationResult<ExternalWindowInfo>> ReadAsync(WindowIdentity identity, CancellationToken cancellationToken = default) =>
        RunAsync<ExternalWindowInfo>((id, token) =>
        {
            token.ThrowIfCancellationRequested();
            if (identity.Hwnd == 0 || identity.ProcessId == 0)
                return OperationResult<ExternalWindowInfo>.Fail(OperationCodes.InvalidArgument, "Invalid identity.", id.ToString("N"));
            var candidate = ReadCandidate(identity.Hwnd, identity.ProcessId, [], ReadElevation((uint)Environment.ProcessId, id), id, out string failureCode);
            if (candidate is null)
            {
                diagnostics.Record(new(DiagnosticEvent.Selection, DiagnosticLevel.Debug, id,
                    failureCode == OperationCodes.WindowNotFound ? DiagnosticCode.WindowGone
                        : failureCode == OperationCodes.AccessDenied ? DiagnosticCode.AccessDenied : DiagnosticCode.NativeFailure,
                    ProcessId: identity.ProcessId, Hwnd: identity.Hwnd));
                return OperationResult<ExternalWindowInfo>.Fail(failureCode, "Window could not be read as an eligible candidate.", id.ToString("N"));
            }
            diagnostics.Record(new(DiagnosticEvent.Selection, DiagnosticLevel.Debug, id,
                ProcessId: identity.ProcessId, Hwnd: identity.Hwnd));
            return OperationResult<ExternalWindowInfo>.Ok(candidate, id.ToString("N"));
        }, cancellationToken);

    private async Task<OperationResult<T>> RunAsync<T>(Func<Guid, CancellationToken, OperationResult<T>> action, CancellationToken token)
    {
        Guid id = Guid.NewGuid();
        try { return await dispatcher.InvokeAsync(() => action(id, token), token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            diagnostics.Record(new(DiagnosticEvent.Discovery, DiagnosticLevel.Debug, id, DiagnosticCode.Cancelled));
            return OperationResult<T>.Fail(OperationCodes.Cancelled, "Operation cancelled.", id.ToString("N"));
        }
        catch (Exception error)
        {
            diagnostics.Record(new(DiagnosticEvent.Discovery, DiagnosticLevel.Error, id,
                DiagnosticCode.InternalError, ExceptionHResult: error.HResult));
            return OperationResult<T>.Fail(OperationCodes.InternalError, "Native discovery failed.", id.ToString("N"));
        }
    }

    private ExternalWindowInfo? ReadCandidate(nint hwnd, uint? expectedPid,
        Dictionary<uint, (string Name, bool? Elevated)> processes, bool? ownElevation, Guid id, out string failureCode)
    {
        failureCode = OperationCodes.WindowNotFound;
        if (!NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd)) return null;
        if (NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid) == 0) return null;
        if (pid == (uint)Environment.ProcessId || (expectedPid is not null && pid != expectedPid)) return null;
        if (!ReadStyle(hwnd, -16, id, out long style, out int styleError))
        {
            failureCode = styleError == 5 ? OperationCodes.AccessDenied : OperationCodes.InternalError;
            return null;
        }
        if (!ReadStyle(hwnd, -20, id, out long extended, out styleError))
        {
            failureCode = styleError == 5 ? OperationCodes.AccessDenied : OperationCodes.InternalError;
            return null;
        }
        if ((style & 0x40000000L) != 0 || (extended & 0x80L) != 0) return null;
        int hr = NativeMethods.DwmGetWindowAttribute(hwnd, 14, out int cloaked, sizeof(int));
        if (hr == 0 && cloaked != 0) return null;
        if (hr != 0) LogNative(id, NativeApi.DwmGetWindowAttribute, hr);
        var text = new StringBuilder(32768);
        NativeMethods.SetLastError(0);
        int count = NativeMethods.GetWindowText(hwnd, text, text.Capacity);
        int error = Marshal.GetLastPInvokeError();
        if (count == 0)
        {
            if (error != 0) LogNative(id, NativeApi.GetWindowText, error);
            return null;
        }
        string title = text.ToString();
        if (string.IsNullOrWhiteSpace(title)) return null;
        text = new StringBuilder(256);
        if (NativeMethods.GetClassName(hwnd, text, text.Capacity) == 0)
            LogNative(id, NativeApi.GetClassName, Marshal.GetLastPInvokeError());
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            int rectError = Marshal.GetLastPInvokeError();
            failureCode = rectError == 5 ? OperationCodes.AccessDenied : OperationCodes.InternalError;
            LogNative(id, NativeApi.GetWindowRect, rectError);
            return null;
        }
        if (!processes.TryGetValue(pid, out var process))
        {
            string name;
            try
            {
                using var target = Process.GetProcessById((int)pid);
                name = target.ProcessName;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
            {
                name = "(unknown)";
                diagnostics.Record(new(DiagnosticEvent.NativeRead, DiagnosticLevel.Debug, id,
                    DiagnosticCode.AccessDenied, ProcessId: pid, ExceptionHResult: exception.HResult));
            }
            process = (name, ReadElevation(pid, id));
            processes.Add(pid, process);
        }
        nint context = NativeMethods.GetWindowDpiAwarenessContext(hwnd);
        DpiMode dpi = context == 0 ? DpiMode.Unknown
            : NativeMethods.AreDpiAwarenessContextsEqual(context, -4) ? DpiMode.PerMonitorV2
            : NativeMethods.GetAwarenessFromDpiAwarenessContext(context) switch
            { 0 => DpiMode.Unaware, 1 => DpiMode.SystemAware, 2 => DpiMode.PerMonitor, _ => DpiMode.Unknown };
        uint actualDpi = NativeMethods.GetDpiForWindow(hwnd);
        bool minimized = NativeMethods.IsIconic(hwnd);
        bool owner = NativeMethods.GetWindow(hwnd, 4) != 0;
        if (!NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd)
            || NativeMethods.GetWindowThreadProcessId(hwnd, out uint finalPid) == 0 || finalPid != pid) return null;
        return new(new(hwnd, pid), title, process.Name, text.ToString(),
            new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), style, extended,
            minimized, owner, process.Elevated, ownElevation, dpi, actualDpi);
    }

    private bool ReadStyle(nint hwnd, int index, Guid id, out long value, out int error)
    {
        NativeMethods.SetLastError(0);
        nint result = NativeMethods.GetWindowLongPtr(hwnd, index);
        error = Marshal.GetLastPInvokeError();
        value = result.ToInt64() & uint.MaxValue;
        if (result != 0 || error == 0) return true;
        LogNative(id, NativeApi.GetWindowLongPtr, error);
        return false;
    }

    private bool? ReadElevation(uint pid, Guid id)
    {
        using var process = NativeMethods.OpenProcess(0x1000, false, pid);
        if (process.IsInvalid) { LogNative(id, NativeApi.OpenProcess, Marshal.GetLastPInvokeError()); return null; }
        if (!NativeMethods.OpenProcessToken(process, 8, out var token))
        { LogNative(id, NativeApi.OpenProcessToken, Marshal.GetLastPInvokeError()); return null; }
        using (token)
        {
            if (!NativeMethods.GetTokenInformation(token, 20, out int elevation, sizeof(int), out _))
            { LogNative(id, NativeApi.GetTokenInformation, Marshal.GetLastPInvokeError()); return null; }
            return elevation != 0;
        }
    }

    private void LogNative(Guid id, NativeApi api, int error) =>
        diagnostics.Record(new(DiagnosticEvent.NativeRead, DiagnosticLevel.Warning, id,
            error == 5 ? DiagnosticCode.AccessDenied : DiagnosticCode.NativeFailure, api, error));
}
