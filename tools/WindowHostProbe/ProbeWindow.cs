using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FlexCanvas.Domain.Poc;
using FlexCanvas.Infrastructure.Win32;

namespace FlexCanvas.WindowHostProbe;

public sealed class ProbeWindow : Window
{
    private readonly string _root = FindRoot();
    private readonly ProbeHostController _controller;
    private readonly ComboBox _candidates = new() { Width = 320, DisplayMemberPath = nameof(ProbeCandidate.Kind) };
    private readonly NativeProbeSurface _surface = new() { Width = 640, Height = 420 };
    private readonly TextBox _log = new() { IsReadOnly = true, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _status = new() { Text = "No probe selected.", Margin = new(8) };
    private readonly DispatcherTimer _health = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _busy, _closing, _canClose;

    public ProbeWindow()
    {
        _controller = new(Path.Combine(_root, "tools"));
        Title = "FlexCanvas - Controlled HWND POC";
        Width = 1100;
        Height = 850;
        MinWidth = 900;
        MinHeight = 720;
        var grid = new Grid { Margin = new(12) };
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), new GridLength(150) })
            grid.RowDefinitions.Add(new() { Height = height });
        Add(grid, new TextBlock
        {
            Text = "Only repository-built probes are allowed. Detach before closing. IME/DPI compatibility is not yet certified.",
            TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 10)
        }, 0);
        var bar = new WrapPanel();
        foreach (string kind in new[] { "Wpf", "WinForms", "Win32" })
            Button(bar, $"Start {kind}", () => StartProbe(kind));
        bar.Children.Add(_candidates);
        Button(bar, "Refresh", () => RunAsync(async () =>
        {
            _candidates.ItemsSource = await _controller.DiscoverAsync();
            _candidates.SelectedIndex = 0;
        }));
        Button(bar, "Embed", () => RunAsync(async () =>
        {
            if (_candidates.SelectedItem is not ProbeCandidate target || _surface.SurfaceHandle == 0)
            {
                Record(ProbeResult.Fail("INVALID_ARGUMENT", "Select a visible probe and wait for the host surface."));
                return;
            }
            Record(await _controller.EmbedAsync(target.Hwnd, _surface.SurfaceHandle));
            UpdateStatus(await _controller.GetStatusAsync());
        }));
        Button(bar, "Detach", () => RunAsync(async () =>
        {
            Record(await _controller.DetachAsync());
            UpdateStatus(await _controller.GetStatusAsync());
        }));
        Button(bar, "Export diagnostics", () => RunAsync(ExportAsync));
        Add(grid, bar, 1);
        var sizes = new StackPanel { Orientation = Orientation.Horizontal };
        Slider(sizes, "Width", 480, 900, 640, value => _surface.Width = value);
        Slider(sizes, "Height", 320, 520, 420, value => _surface.Height = value);
        Slider(sizes, "Move X", 0, 120, 0, value => Canvas.SetLeft(_surface, value));
        Slider(sizes, "Move Y", 0, 70, 0, value => Canvas.SetTop(_surface, value));
        Add(grid, sizes, 2);
        var canvas = new Canvas { Background = Brushes.LightGray, ClipToBounds = true };
        Canvas.SetLeft(_surface, 0);
        Canvas.SetTop(_surface, 0);
        canvas.Children.Add(_surface);
        Add(grid, canvas, 3);
        var diagnostics = new DockPanel();
        DockPanel.SetDock(_status, Dock.Top);
        diagnostics.Children.Add(_status);
        diagnostics.Children.Add(_log);
        Add(grid, diagnostics, 4);
        Content = grid;
        _surface.SizeChanged += (_, _) => RunAsync(async () =>
        {
            if ((await _controller.GetStatusAsync())?.State == EmbeddingState.Embedded)
            {
                var result = await _controller.ResizeAsync();
                if (!result.Success) Record(result);
            }
        });
        _health.Tick += (_, _) => RunAsync(async () =>
        {
            var state = await _controller.GetStatusAsync();
            if (state?.State == EmbeddingState.Embedded)
            {
                var result = await _controller.ResizeAsync();
                if (!result.Success) Record(result);
            }
            UpdateStatus(state);
        });
        Loaded += (_, _) => _health.Start();
        Closing += OnClosing;
    }

    private async void RunAsync(Func<Task> action)
    {
        if (_busy || _closing) return;
        _busy = true;
        try { await action(); }
        catch (Exception error) { Record(ProbeResult.Fail("UNEXPECTED_ERROR", error.GetType().Name)); }
        finally { _busy = false; }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_canClose) return;
        e.Cancel = true;
        if (_closing || _busy) return;
        _closing = true;
        try
        {
            var result = await _controller.DetachAsync();
            Record(result);
            if (!result.Success)
            {
                MessageBox.Show(this, "Restoration failed. The host remains open so you can retry Detach.", "Restore required");
                return;
            }
            _health.Stop();
            await _controller.DisposeAsync();
            _canClose = true;
            Close();
        }
        catch (Exception error) { Record(ProbeResult.Fail("DETACH_FAILED", error.GetType().Name)); }
        finally { _closing = false; }
    }

    private void StartProbe(string kind) => RunAsync(() =>
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string path = Path.Combine(_root, "tools", "Probes", $"{kind}Probe", "bin", configuration,
            "net8.0-windows", $"FlexCanvas.{kind}Probe.exe");
        if (!File.Exists(path)) Record(ProbeResult.Fail("WINDOW_NOT_FOUND", "Build the solution before starting this probe."));
        else
        {
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            Record(ProbeResult.Ok($"Started controlled {kind} probe. Refresh after its window appears."));
        }
        return Task.CompletedTask;
    });

    private async Task ExportAsync()
    {
        string text = _log.Text + Environment.NewLine + _status.Text;
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlexCanvas", "logs");
        await Task.Run(() => Directory.CreateDirectory(folder));
        await File.WriteAllTextAsync(Path.Combine(folder, $"poc-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.log"), text);
        Record(ProbeResult.Ok("Local diagnostics exported. No network upload."));
    }

    private void Record(ProbeResult result)
    {
        _log.AppendText($"{DateTime.UtcNow:O} {result.Code} Win32={result.Win32Error?.ToString() ?? "-"} {result.Message}{Environment.NewLine}");
        if (_log.Text.Length > 24000) _log.Text = _log.Text[^16000..];
        _log.ScrollToEnd();
    }

    private void UpdateStatus(ProbeStatus? state) => _status.Text = state is null ? "No session."
        : $"{state.State}; session={state.SessionId}; PID={state.Original.ProcessId}; original DPI={state.Original.Dpi}; HWND=0x{state.Original.Hwnd:X}";

    private static void Add(Grid grid, UIElement child, int row) { Grid.SetRow(child, row); grid.Children.Add(child); }
    private static void Button(Panel panel, string label, Action action)
    {
        var button = new Button { Content = label, Margin = new(4), Padding = new(8, 4, 8, 4) };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
    }
    private static void Slider(Panel panel, string label, double min, double max, double value, Action<double> update)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new(8, 4, 2, 4) });
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, Width = 105, Margin = new(2, 4, 8, 4) };
        slider.ValueChanged += (_, e) => update(e.NewValue);
        panel.Children.Add(slider);
    }
    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "FlexCanvas.sln"))) return directory.FullName;
        throw new InvalidOperationException("Repository solution could not be found.");
    }
}
