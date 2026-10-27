using FlexCanvas.Infrastructure.Win32;

namespace FlexCanvas.Win32Probe;

internal static class Program
{
    [STAThread]
    private static int Main() => NativeProbeRunner.Run();
}
