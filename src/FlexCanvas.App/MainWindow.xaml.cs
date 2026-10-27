using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using FlexCanvas.Application;
using FlexCanvas.Domain;

namespace FlexCanvas.App;

public partial class MainWindow : System.Windows.Window
{
    private readonly WindowManagerViewModel _viewModel;
    private readonly IWindowEmbeddingService _embedding;
    private readonly IWindowCompatibilityService _compatibility;
    private readonly List<NativeHostingWindow> _hosts = [];
    private readonly ContainerSelectionModel _selection = new();
    private readonly Dictionary<nint, string> _windowNames = [];
    private Task _sessionsPending = Task.CompletedTask;
    private bool _updatingSelection;
    private Task _embedPending = Task.CompletedTask;
    private readonly DispatcherTimer _selectedTargetTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _closing;
    private bool _closedSafely;

    public MainWindow(WindowManagerViewModel viewModel, IWindowEmbeddingService embedding, IWindowCompatibilityService compatibility)
    {
        _viewModel = viewModel;
        _embedding = embedding;
        _compatibility = compatibility;
        InitializeComponent();
        DataContext = viewModel;
        SpatialCanvas.AttachServices(embedding);
        Loaded += OnLoaded;
        Closing += OnClosing;
        _selectedTargetTimer.Tick += (_, _) =>
        {
            if (_viewModel.CheckCommand.CanExecute(null)) _viewModel.CheckCommand.Execute(null);
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _viewModel.RefreshCommand.Execute(null);
        _selectedTargetTimer.Start();
    }

    private async void OnRefreshSessions(object sender, RoutedEventArgs e)
    {
        if (_closing || !_sessionsPending.IsCompleted) return;
        _sessionsPending = RefreshSessionsAsync();
        await _sessionsPending;
    }

    private async Task RefreshSessionsAsync()
    {
        try
        {
            var sessions = await _embedding.GetSessionsAsync();
            _selection.Update(sessions);
            // Display names are runtime-only; prune entries whose sessions no longer exist.
            var liveHwnds = sessions.Where(item => item.NeedsRestore)
                .Select(item => item.Original.Identity.Hwnd).ToHashSet();
            foreach (var stale in _windowNames.Keys.Where(hwnd => !liveHwnds.Contains(hwnd)).ToArray())
                _windowNames.Remove(stale);
            SpatialCanvas.UpdateSessions(sessions, _windowNames);
            _updatingSelection = true;
            try
            {
                SessionGrid.ItemsSource = _selection.Sessions;
                SessionGrid.SelectedItem = _selection.Selected;
            }
            finally { _updatingSelection = false; }
            SessionStatus.Text = $"真实活动/待恢复会话：{_selection.Sessions.Count}；选中：{_selection.SelectedId?.ToString("N") ?? "无"}";
        }
        catch (Exception error)
        {
            _selection.Update([]);
            SessionGrid.ItemsSource = _selection.Sessions;
            SessionStatus.Text = $"读取失败（0x{error.HResult:X}）；未解除任何目标，请检查宿主。";
        }
    }

    private void OnSessionSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_updatingSelection) return;
        _selection.Select((SessionGrid.SelectedItem as EmbeddingSessionStatus)?.SessionId);
        SessionStatus.Text = $"选中会话：{_selection.SelectedId?.ToString("N") ?? "无"}";
    }

    private async void OnLocateHost(object sender, RoutedEventArgs e)
    {
        if (_closing || !_sessionsPending.IsCompleted) return;
        _sessionsPending = LocateHostAsync();
        await _sessionsPending;
    }

    private async Task LocateHostAsync()
    {
        Guid? requested = _selection.SelectedId;
        await RefreshSessionsAsync();
        if (_closing || requested is null || _selection.SelectedId != requested || _selection.Selected is not { } session) return;
        var host = _hosts.FirstOrDefault(item => item.HostSurface == session.Host && !item.IsClosing);
        if (host is null) { SessionStatus.Text = "此会话没有可定位的现有宿主；未修改目标。"; return; }
        if (host.WindowState == WindowState.Minimized) host.WindowState = WindowState.Normal;
        host.Activate();
    }

    private async void OnEmbedClick(object sender, RoutedEventArgs e)
    {
        if (_closing || !_embedPending.IsCompleted) return;
        _embedPending = EmbedSelectedAsync();
        await _embedPending;
    }

    private async Task EmbedSelectedAsync()
    {
        if (_viewModel.SelectedWindow is not { } selected)
        {
            _viewModel.Status = "请先选择外部窗口。";
            return;
        }
        try
        {
            var report = await _compatibility.CheckAsync(selected.Identity);
            if (_closing) return;
            if (!report.Success || report.Data is not { CanAttempt: true })
            {
                _viewModel.Status = $"不能建议嵌入；{report.Code} ID: {report.CorrelationId}";
                MessageBox.Show(report.Data is { } risk ? string.Join("\n", risk.Reasons) : _viewModel.Status,
                    "FlexCanvas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show($"将真实修改并承载此窗口：\n{selected.Title}\n{selected.DisplayIdentity}\n"
                + "兼容性不保证。关闭承载时将尝试恢复；恢复失败会保留宿主供重试。继续？",
                "确认嵌入", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes || _closing) return;
            // Runtime display name for the overview; not a persisted identity and never logged.
            _windowNames[selected.Identity.Hwnd] =
                string.IsNullOrWhiteSpace(selected.Title) ? selected.ProcessName : selected.Title;
            // Recovery is managed by _hosts, not native ownership: owned windows stay above the main window.
            var host = new NativeHostingWindow(_embedding, selected.Identity, () =>
            {
                if (_closing) return;
                if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                Activate();
            });
            _hosts.Add(host);
            host.Closed += (_, _) => _hosts.Remove(host);
            host.Show();
        }
        catch (Exception error) { _viewModel.Status = $"打开承载失败（0x{error.HResult:X}），请检查已有承载窗口。"; }
    }

    private async void OnEmbedToCanvasClick(object sender, RoutedEventArgs e)
    {
        if (_closing || !_embedPending.IsCompleted) return;
        _embedPending = EmbedToCanvasAsync();
        await _embedPending;
    }

    private async Task EmbedToCanvasAsync()
    {
        if (_viewModel.SelectedWindow is not { } selected)
        {
            _viewModel.Status = "请先选择外部窗口。";
            return;
        }
        try
        {
            var report = await _compatibility.CheckAsync(selected.Identity);
            if (_closing) return;
            if (!report.Success || report.Data is not { CanAttempt: true })
            {
                _viewModel.Status = $"不能建议嵌入；{report.Code} ID: {report.CorrelationId}";
                MessageBox.Show(report.Data is { } risk ? string.Join("\n", risk.Reasons) : _viewModel.Status,
                    "FlexCanvas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show($"将真实修改并承载此窗口到空间画布（1:1原生模式）：\n{selected.Title}\n{selected.DisplayIdentity}\n"
                + "兼容性不保证。画布缩放/平移只改变视图，不改变窗口实际尺寸；关闭时恢复原窗口；恢复失败保留宿主供重试。继续？",
                "确认嵌入到画布", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes || _closing) return;
            _windowNames[selected.Identity.Hwnd] =
                string.IsNullOrWhiteSpace(selected.Title) ? selected.ProcessName : selected.Title;
            // Connect the canvas page first: a HwndHost outside the selected tab cannot build its HWND.
            MainTabs.SelectedItem = MainTabs.Items[1];
            SpatialCanvas.UpdateLayout();
            var surface = SpatialCanvas.PrepareNativeHost();
            if (surface == 0)
            {
                _viewModel.Status = "画布承载面创建失败；目标未被修改。";
                return;
            }
            var result = await _embedding.EmbedAsync(selected.Identity, surface);
            if (!result.Success)
            {
                _viewModel.Status = $"画布承载失败（{result.Code} 0x{result.Win32ErrorCode:X}）ID: {result.CorrelationId}；目标保持未承载或已回滚。";
                await RefreshSessionsAsync();
                return;
            }
            _viewModel.Status = $"画布承载成功 ID: {result.CorrelationId}；空间画布内以原始尺寸显示。";
            MainTabs.SelectedItem = MainTabs.Items[1];
            SpatialCanvas.SetNativeMode(true);
            await RefreshSessionsAsync();
        }
        catch (Exception error)
        {
            _viewModel.Status = $"画布承载失败（0x{error.HResult:X}）；请刷新会话并检查画布承载面。";
            await RefreshSessionsAsync();
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closedSafely) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        _selectedTargetTimer.Stop();
        IsEnabled = false;
        try
        {
            await _embedPending;
            await _sessionsPending;
            foreach (var host in _hosts.ToArray())
                if (!await host.RestoreAndCloseAsync()) throw new InvalidOperationException("A host could not restore its target.");
            if (!await SpatialCanvas.DetachAllAsync())
                throw new InvalidOperationException("A canvas session could not restore its target.");
            await ((App)System.Windows.Application.Current).PrepareShutdownAsync();
            await _viewModel.DisposeAsync();
            await ((App)System.Windows.Application.Current).StopServicesAsync();
        }
        catch (Exception error)
        {
            MessageBox.Show($"关闭未完成（0x{error.HResult:X}）；保留主窗口和未恢复宿主，请重试解除。", "FlexCanvas", MessageBoxButton.OK, MessageBoxImage.Error);
            _closing = false;
            IsEnabled = true;
            _selectedTargetTimer.Start();
            return;
        }
        _closedSafely = true;
        Close();
    }
}
