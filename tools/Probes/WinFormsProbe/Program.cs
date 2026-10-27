namespace FlexCanvas.WinFormsProbe;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        using var form = new Form { Text = "FlexCanvas WinForms Probe", Width = 640, Height = 480, MinimumSize = new(320, 240) };
        var menu = new MenuStrip();
        var actions = new ToolStripMenuItem("Actions");
        actions.DropDownItems.Add("Open dialog", null, (_, _) => MessageBox.Show(form, "Controlled WinForms dialog.", "Probe dialog"));
        actions.DropDownItems.Add("Close probe", null, (_, _) => form.Close());
        menu.Items.Add(actions);
        var choices = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        choices.Items.AddRange(["Alpha", "Beta", "Gamma"]);
        choices.SelectedIndex = 0;
        var text = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both,
            Text = "WinForms text probe. Test IME, selection, Ctrl+C/V, right-click and scrolling."
        };
        form.Controls.Add(text);
        form.Controls.Add(choices);
        form.Controls.Add(menu);
        form.MainMenuStrip = menu;
        System.Windows.Forms.Application.Run(form);
    }
}
