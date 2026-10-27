using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FlexCanvas.Infrastructure.Win32;

/// <summary>A standalone native HWND target with standard Win32 edit and button controls.</summary>
public static class NativeProbeRunner
{
    private static readonly NativeMethods.WindowProc Procedure = ProcessMessage;

    /// <summary>Runs the native probe message pump on the entry thread.</summary>
    public static int Run()
    {
        var definition = new NativeMethods.WindowClass
        {
            Name = "FlexCanvas.NativeProbe",
            Procedure = Procedure,
            Instance = NativeMethods.GetModuleHandle(null),
            Background = 6
        };
        if (NativeMethods.RegisterClass(ref definition) == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "RegisterClassW failed.");
        nint window = NativeMethods.CreateWindowEx(0, definition.Name, "FlexCanvas Win32 Probe",
            0x10CF0000, 100, 100, 640, 480, 0, 0, definition.Instance, 0);
        if (window == 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "CreateWindowExW failed.");
        CreateControl("STATIC", "Native HWND probe. Test text input, selection and scrolling.", 0, 16, 12, 560, 30, window);
        CreateControl("EDIT", "Type and test Ctrl+C / Ctrl+V here.", 0x00A010C4, 16, 52, 550, 220, window);
        CreateControl("BUTTON", "Native button", 0, 16, 290, 150, 36, window);
        int result;
        while ((result = NativeMethods.GetMessage(out var message, 0, 0, 0)) > 0)
        {
            NativeMethods.TranslateMessage(in message);
            NativeMethods.DispatchMessage(in message);
        }
        if (result == -1) throw new Win32Exception(Marshal.GetLastPInvokeError(), "GetMessageW failed.");
        GC.KeepAlive(Procedure);
        return 0;
    }

    private static void CreateControl(string kind, string text, uint extra, int x, int y, int width, int height, nint parent)
    {
        if (NativeMethods.CreateWindowEx(0, kind, text, 0x50010000 | extra, x, y, width, height, parent, 0, 0, 0) == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Creating probe control failed.");
    }

    private static nint ProcessMessage(nint hwnd, uint message, nint wparam, nint lparam)
    {
        if (message == 0x0002)
        {
            NativeMethods.PostQuitMessage(0);
            return 0;
        }
        return NativeMethods.DefWindowProc(hwnd, message, wparam, lparam);
    }
}
