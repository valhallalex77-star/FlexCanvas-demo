using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace FlexCanvas.Infrastructure.Win32;

internal static class NativeMethods
{
    internal delegate bool EnumWindowProc(nint hwnd, nint parameter);
    internal delegate nint WindowProc(nint hwnd, uint message, nint wparam, nint lparam);
    internal delegate void WinEventProc(nint hook, uint eventId, nint hwnd, int objectId, int childId, uint threadId, uint time);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Placement
    {
        public uint Length, Flags, ShowCommand;
        public Point MinPosition, MaxPosition;
        public Rect NormalPosition;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct GuiThreadInfo
    {
        public uint Size, Flags;
        public nint Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public Rect CaretRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { public int X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WindowClass
    {
        public uint Style;
        public WindowProc Procedure;
        public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background;
        public string? Menu;
        public string Name;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        public nint Hwnd;
        public uint Id;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int X, Y;
        public uint Private;
    }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(nint hwnd, ref Point point);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ScreenToClient(nint hwnd, ref Point point);
    [DllImport("user32.dll")]
    internal static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll")]
    internal static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetParent(nint hwnd, nint parent);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowProc callback, nint parameter);
    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetWindowText(nint hwnd, StringBuilder text, int maximum);
    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetClassName(nint hwnd, StringBuilder text, int maximum);
    [DllImport("user32.dll", EntryPoint = "SetWindowTextW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowText(nint hwnd, string text);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(nint hwnd, uint attribute, out int value, int size);
    [DllImport("user32.dll")]
    internal static extern int GetAwarenessFromDpiAwarenessContext(nint context);
    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")]
    internal static extern nint GetWindowDpiAwarenessContext(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AreDpiAwarenessContextsEqual(nint first, nint second);
    [DllImport("kernel32.dll")]
    internal static extern void SetLastError(uint error);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetTokenInformation(SafeAccessTokenHandle token, int kind, out int elevation, int length, out int returned);
    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetTokenInformationBuffer(SafeAccessTokenHandle token, int kind, nint buffer, int length, out int returned);
    [DllImport("advapi32.dll")]
    internal static extern nint GetSidSubAuthorityCount(nint sid);
    [DllImport("advapi32.dll")]
    internal static extern nint GetSidSubAuthority(nint sid, uint index);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowPlacement(nint hwnd, ref Placement placement);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPlacement(nint hwnd, in Placement placement);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWinEventHook(uint minimum, uint maximum, nint module, WinEventProc callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint hook);
    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AttachThreadInput(uint from, uint to, [MarshalAs(UnmanagedType.Bool)] bool attach);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetFocus(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateWindowEx(uint extended, string className, string title, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(nint hwnd);
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", EntryPoint = "RegisterClassW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClass(ref WindowClass definition);
    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static extern nint DefWindowProc(nint hwnd, uint message, nint wparam, nint lparam);
    [DllImport("user32.dll")]
    internal static extern void PostQuitMessage(int code);
    [DllImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
    internal static extern int GetMessage(out Message message, nint hwnd, uint minimum, uint maximum);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(in Message message);
    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    internal static extern nint DispatchMessage(in Message message);
    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(nint hwnd, uint message, nint wparam, nint lparam);
}
