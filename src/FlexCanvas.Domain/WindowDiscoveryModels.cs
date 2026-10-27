namespace FlexCanvas.Domain;

public static class OperationCodes
{
    public const string Ok = "OK";
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string WindowNotFound = "WINDOW_NOT_FOUND";
    public const string WindowNotVisible = "WINDOW_NOT_VISIBLE";
    public const string AccessDenied = "ACCESS_DENIED";
    public const string Cancelled = "OPERATION_CANCELLED";
    public const string InternalError = "INTERNAL_ERROR";
    public const string WindowNotSupported = "WINDOW_NOT_SUPPORTED";
    public const string DpiMismatch = "DPI_MISMATCH";
    public const string EmbeddingFailed = "EMBEDDING_FAILED";
    public const string DetachFailed = "DETACH_FAILED";
}

public sealed record OperationResult<T>(bool Success, string Code, string Message,
    T? Data = default, string? CorrelationId = null)
{
    public static OperationResult<T> Ok(T data, string id) => new(true, OperationCodes.Ok, "Completed.", data, id);
    public static OperationResult<T> Fail(string code, string message, string id) => new(false, code, message, default, id);
}

public readonly record struct WindowIdentity(nint Hwnd, uint ProcessId);
public readonly record struct WindowBounds(int X, int Y, int Width, int Height);
public enum DpiMode { Unknown, Unaware, SystemAware, PerMonitor, PerMonitorV2 }
public enum RiskLevel { Low, Medium, High, Unknown }

public sealed record ExternalWindowInfo(WindowIdentity Identity, string Title, string ProcessName,
    string ClassName, WindowBounds Bounds, long Style, long ExtendedStyle, bool IsMinimized,
    bool HasOwner, bool? IsElevated, bool? HostIsElevated, DpiMode DpiMode, uint Dpi)
{
    public string DisplayIdentity => $"0x{Identity.Hwnd:X} / PID {Identity.ProcessId}";
}

public sealed record CompatibilityReport(bool CanAttempt, RiskLevel RiskLevel,
    IReadOnlyList<string> Reasons, IReadOnlyList<string> Warnings, string RecommendedFallback);

public enum DiagnosticEvent { Startup, Shutdown, Discovery, Selection, NativeRead, CommandFailure, UnhandledException,
    Embedding, Detaching, Rollback, TargetExited, WindowEvent }
public enum DiagnosticLevel { Debug, Information, Warning, Error, Critical }
public enum NativeApi { None, EnumWindows, GetWindowThreadProcessId, GetWindowRect, GetWindowLongPtr,
    GetWindowText, GetClassName, OpenProcess, OpenProcessToken, GetTokenInformation, DwmGetWindowAttribute,
    SetWindowLongPtr, SetParent, SetWindowPos, GetClientRect, GetWindowPlacement, SetWindowPlacement,
    GetProcessTimes, SetWinEventHook, UnhookWinEvent, AttachThreadInput, SetFocus, GetGuiThreadInfo }
public enum DiagnosticCode { Ok, Cancelled, WindowGone, AccessDenied, NativeFailure, InternalError }
public sealed record DiagnosticEntry(DiagnosticEvent Event, DiagnosticLevel Level, Guid CorrelationId,
    DiagnosticCode Code = DiagnosticCode.Ok, NativeApi Api = NativeApi.None, int? NativeError = null,
    uint? ProcessId = null, nint? Hwnd = null, int? ExceptionHResult = null);
