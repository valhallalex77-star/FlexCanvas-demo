using System.Windows;
using System.Windows.Controls;

namespace FlexCanvas.WpfProbe;

public partial class ProbeApp : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new Window { Title = "FlexCanvas WPF Probe", Width = 640, Height = 480, MinWidth = 320, MinHeight = 240 };
        var panel = new DockPanel { Margin = new(16) };
        var menu = new Menu();
        var actions = new MenuItem { Header = "_Actions" };
        var dialog = new MenuItem { Header = "_Open dialog" };
        dialog.Click += (_, _) => MessageBox.Show(window, "Controlled WPF dialog.", "Probe dialog");
        var close = new MenuItem { Header = "_Close probe" };
        close.Click += (_, _) => window.Close();
        actions.Items.Add(dialog);
        actions.Items.Add(close);
        menu.Items.Add(actions);
        DockPanel.SetDock(menu, Dock.Top);
        panel.Children.Add(menu);
        var choices = new ComboBox { ItemsSource = new[] { "Alpha", "Beta", "Gamma" }, SelectedIndex = 0, Margin = new(0, 8, 0, 8) };
        DockPanel.SetDock(choices, Dock.Top);
        panel.Children.Add(choices);
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 50, Margin = new(0, 8, 0, 8) };
        DockPanel.SetDock(slider, Dock.Top);
        panel.Children.Add(slider);
        var context = new ContextMenu();
        var select = new MenuItem { Header = "Select all" };
        var text = new TextBox
        {
            Text = "WPF text probe. Test IME, Ctrl+C/V, selection, right-click and scrolling.",
            AcceptsReturn = true, AcceptsTab = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping = TextWrapping.Wrap, ContextMenu = context
        };
        select.Click += (_, _) => text.SelectAll();
        context.Items.Add(select);
        panel.Children.Add(text);
        window.Content = panel;
        window.Show();
    }
}
