using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FlexCanvas.Application;
using FlexCanvas.Domain;
using FlexCanvas.Infrastructure.Win32;

namespace FlexCanvas.App;

/// <summary>S2 input/restore host, deliberately independent of a spatial canvas.</summary>
public sealed class NativeHostingWindow : Window
{
    private readonly IWindowEmbeddingService _service;
    private readonly WindowIdentity _target;
    private readonly NativeWindowHost _surface = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(10) };
    private readonly Button _detach = new() { Content = "解除并关闭承载", Margin = new(8), Padding = new(10, 5, 10, 5) };
    private readonly Button _focus = new() { Content = "聚焦应用", Margin = new(8), Padding = new(10, 5, 10, 5) };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Task _pending = Task.CompletedTask;
    private Guid? _session;
    private bool _busy, _closing, _closedSafely;
    public bool IsClosing => _closing;

    /// <summary>Native surface identity used to locate an existing host without relying on PID or title.</summary>
    public nint HostSurface => _surface.SurfaceHandle;

    public NativeHostingWindow(IWindowEmbeddingService service, WindowIdentity target, Action? returnToMain = null)
    {
        _service = service;
        _target = target;
        Title = $"FlexCanvas Host - PID {target.ProcessId}";
        Width = 850; Height = 650; MinWidth = 480; MinHeight = 360;
        var grid = new Grid();
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new());
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
        toolbar.Children.Add(_detach); toolbar.Children.Add(_focus);
        if (returnToMain is not null)
        {
            var returnButton = new Button { Content = "返回 FlexCanvas", Margin = new(8), Padding = new(10, 5, 10, 5) };
            returnButton.Click += (_, _) => { if (!_closing) returnToMain(); };
            System.Windows.Automation.AutomationProperties.SetAutomationId(returnButton, "ReturnToMain");
            toolbar.Children.Add(returnButton);
        }
        grid.Children.Add(toolbar);
        Grid.SetRow(_surface, 1); grid.Children.Add(_surface);
        Grid.SetRow(_status, 2); grid.Children.Add(_status);
        Content = grid;
        Loaded += OnLoaded;
        SizeChanged += (_, _) => StartOperation(() => ResizeAsync());
        _timer.Tick += (_, _) => StartOperation(UpdateStatusAsync);
        _focus.Click += (_, _) => StartOperation(async () =>
        {
            if (_session is { } id) ShowResult(await _service.FocusAsync(id));
        });
        _detach.Click += async (_, _) => await RestoreAndCloseAsync();
        Closing += OnClosing;
        System.Windows.Automation.AutomationProperties.SetAutomationId(_status, "HostStatus");
        System.Windows.Automation.AutomationProperties.SetAutomationId(_detach, "DetachHost");
    }
    private void StartOperation(Func<Task> operation)
    {
        if (_busy || _closing) return;
        _pending = RunOperationAsync(operation);
    }
    private async Task RunOperationAsync(Func<Task> operation)
    {
        _busy = true;
        _focus.IsEnabled = false;
        try { await operation(); }
        catch (Exception error) { _status.Text = $"Operation failed (0x{error.HResult:X}); keep host open and retry Detach."; }
        finally { _busy = false; _focus.IsEnabled = !_closing && _session is not null; }
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        StartOperation(async () =>
        {
            var result = await _service.EmbedAsync(_target, _surface.SurfaceHandle);
            _session = result.Session?.SessionId;
            if (_session is null)
                _session = (await _service.GetSessionsAsync()).FirstOrDefault(s => s.Host == _surface.SurfaceHandle && s.NeedsRestore)?.SessionId;
            ShowResult(result);
            _timer.Start();
        });
    }
    private async Task ResizeAsync()
    {
        if (_session is { } id && WindowState != WindowState.Minimized) ShowResult(await _service.ResizeAsync(id));
    }
    private async Task UpdateStatusAsync()
    {
        if (_session is not { } id) return;
        var state = (await _service.GetSessionsAsync()).FirstOrDefault(s => s.SessionId == id);
        if (state?.State == WindowEmbeddingState.Embedded && WindowState != WindowState.Minimized)
        {
            var resized = await _service.ResizeAsync(id);
            if (!resized.Success) { ShowResult(resized); return; }
        }
        if (state is not null)
            _status.Text = $"{state.State} | {state.LastResult.Code} | ID: {id:N}";
    }
    private void ShowResult(EmbeddingResult result) =>
        _status.Text = $"{result.Session?.State.ToString() ?? "No session"} | {result.Code}\n{result.Message}\n"
            + $"API: {result.Api} Error: {result.Win32ErrorCode} ID: {result.CorrelationId}";
    public async Task<bool> RestoreAndCloseAsync()
    {
        if (_closedSafely) return true;
        if (_closing) return false;
        _closing = true;
        _timer.Stop();
        _detach.IsEnabled = false; _focus.IsEnabled = false;
        try
        {
            await _pending;
            // Always query by host: an unexpected service exception must not lose a recovery session.
            var sessions = await _service.GetSessionsAsync();
            foreach (var state in sessions.Where(s => s.Host == _surface.SurfaceHandle && s.NeedsRestore))
            {
                var result = await _service.DetachAsync(state.SessionId);
                ShowResult(result);
                if (!result.Success) throw new InvalidOperationException("Recovery failed; keep host and retry.");
            }
            _closedSafely = true;
            Close();
            return true;
        }
        catch (Exception error)
        {
            MessageBox.Show($"解除失败，承载窗口将保持打开，可点击解除重试。\n错误：0x{error.HResult:X}", "FlexCanvas", MessageBoxButton.OK, MessageBoxImage.Warning);
            _closing = false; _detach.IsEnabled = true; _focus.IsEnabled = _session is not null; _timer.Start();
            return false;
        }
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closedSafely) return;
        e.Cancel = true;
        await RestoreAndCloseAsync();
    }
}
