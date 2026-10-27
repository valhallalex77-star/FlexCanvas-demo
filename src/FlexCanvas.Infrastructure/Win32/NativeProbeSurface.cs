using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace FlexCanvas.Infrastructure.Win32;

/// <summary>Interop-only HwndHost surface; it never embeds a target by itself.</summary>
public sealed class NativeProbeSurface : HwndHost
{
    /// <summary>The owned client HWND, available after WPF builds the control.</summary>
    public nint SurfaceHandle { get; private set; }

    /// <inheritdoc />
    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        SurfaceHandle = NativeMethods.CreateWindowEx(0, "STATIC", "", 0x50000000 | 0x02000000,
            0, 0, 640, 420, hwndParent.Handle, 0, 0, 0);
        if (SurfaceHandle == 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "CreateWindowExW failed.");
        return new(this, SurfaceHandle);
    }

    /// <inheritdoc />
    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (!NativeMethods.DestroyWindow(hwnd.Handle))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "DestroyWindow failed.");
        SurfaceHandle = 0;
    }
}
