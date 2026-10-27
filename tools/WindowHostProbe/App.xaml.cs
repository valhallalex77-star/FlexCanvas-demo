namespace FlexCanvas.WindowHostProbe;

public partial class ProbeApp : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        new ProbeWindow().Show();
    }
}
