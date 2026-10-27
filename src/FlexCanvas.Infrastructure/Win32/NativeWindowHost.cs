using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace FlexCanvas.Infrastructure.Win32;

/// <summary>Production HWND surface; refuses to destroy a host with an unrecovered child.</summary>
public sealed class NativeWindowHost : HwndHost
{
    public nint SurfaceHandle { get; private set; }
    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        SurfaceHandle = NativeMethods.CreateWindowEx(0, "STATIC", "", 0x52000000,
            0, 0, 640, 420, hwndParent.Handle, 0, 0, 0);
        if (SurfaceHandle == 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "CreateWindowExW failed.");
        return new(this, SurfaceHandle);
    }
    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (NativeMethods.GetWindow(hwnd.Handle, 5) != 0)
            throw new InvalidOperationException("Detach the external child before destroying its host.");
        if (!NativeMethods.DestroyWindow(hwnd.Handle)) throw new Win32Exception(Marshal.GetLastPInvokeError(), "DestroyWindow failed.");
        SurfaceHandle = 0;
    }

    /// <summary>Moves a host surface to the top of the sibling Z order without activation; used for the selected container.</summary>
    public static void MoveSurfaceToTop(nint surface)
    {
        if (surface == 0 || !NativeMethods.IsWindow(surface)) return;
        // SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE; HWND_TOP = 0.
        NativeMethods.SetWindowPos(surface, 0, 0, 0, 0, 0, 0x0013);
    }
}
