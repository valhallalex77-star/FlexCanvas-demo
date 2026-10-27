using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexCanvas.Domain;

namespace FlexCanvas.Application;

public sealed partial class WindowManagerViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IWindowDiscoveryService _discovery;
    private readonly IWindowCompatibilityService _compatibility;
    private readonly IDiagnosticSink _diagnostics;
    private readonly CancellationTokenSource _lifetime = new();
    private IReadOnlyList<ExternalWindowInfo> _all = [];
    private long _selectionVersion;
    private bool _stopping;

    public WindowManagerViewModel(IWindowDiscoveryService discovery, IWindowCompatibilityService compatibility, IDiagnosticSink diagnostics)
    {
        _discovery = discovery;
        _compatibility = compatibility;
        _diagnostics = diagnostics;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy && !_stopping);
        CheckCommand = new AsyncRelayCommand(CheckAsync, () => !IsBusy && SelectedWindow is not null && !_stopping);
    }

    public ObservableCollection<ExternalWindowInfo> Windows { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand CheckCommand { get; }
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private ExternalWindowInfo? _selectedWindow;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "点击刷新以发现当前桌面的外部窗口。";
    [ObservableProperty] private string _reportText = "请选择窗口。风险检查只读，嵌入须再次确认。";
    public bool IsIdle => !IsBusy;
    public bool HasWindows => Windows.Count > 0;
    public string LogStatus => _diagnostics.IsHealthy ? "本地诊断日志正常" : "诊断日志不可用或有记录丢失";

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
        Status = $"发现 {_all.Count} 个窗口，搜索结果 {Windows.Count} 个。";
    }
    partial void OnSelectedWindowChanged(ExternalWindowInfo? value)
    {
        _selectionVersion++;
        ReportText = value is null ? "请选择窗口。" : "尚未检查当前目标；点击检查或等待选中目标更新。";
        CheckCommand.NotifyCanExecuteChanged();
    }
    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsIdle));
        RefreshCommand.NotifyCanExecuteChanged();
        CheckCommand.NotifyCanExecuteChanged();
    }

    private void ApplyFilter()
    {
        var selected = SelectedWindow?.Identity;
        string query = SearchText.Trim();
        var visible = _all.Where(w => query.Length == 0
            || w.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || w.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || w.DisplayIdentity.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        Windows.Clear();
        foreach (var window in visible) Windows.Add(window);
        SelectedWindow = visible.FirstOrDefault(w => w.Identity == selected);
        OnPropertyChanged(nameof(HasWindows));
    }

    private async Task RefreshAsync(CancellationToken token)
    {
        if (_stopping) return;
        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
        try
        {
            var result = await _discovery.EnumerateAsync(linked.Token);
            if (_stopping) return;
            // Failure removes stale actionable candidates instead of retaining a misleading list.
            _all = result.Success ? result.Data ?? [] : [];
            ApplyFilter();
            Status = result.Success ? $"发现 {_all.Count} 个窗口，搜索结果 {Windows.Count} 个。" : FriendlyError(result.Code, result.CorrelationId);
        }
        catch (OperationCanceledException) { Status = "操作已取消。"; }
        catch (Exception error) { HandleCommandFailure(error); _all = []; ApplyFilter(); }
        finally { IsBusy = false; OnPropertyChanged(nameof(LogStatus)); }
    }

    private async Task CheckAsync(CancellationToken token)
    {
        if (_stopping) return;
        if (SelectedWindow is not { } selected) return;
        long version = _selectionVersion;
        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
        try
        {
            var result = await _compatibility.CheckAsync(selected.Identity, linked.Token);
            if (_stopping || version != _selectionVersion) return;
            if (!result.Success)
            {
                if (result.Code is OperationCodes.WindowNotFound or OperationCodes.WindowNotVisible)
                {
                    _all = _all.Where(w => w.Identity != selected.Identity).ToArray();
                    ApplyFilter();
                    SelectedWindow = null;
                }
                ReportText = FriendlyError(result.Code, result.CorrelationId);
                Status = ReportText;
                return;
            }
            var report = result.Data!;
            ReportText = $"可建议尝试：{(report.CanAttempt ? "是（不保证兼容）" : "否")}\n风险：{report.RiskLevel}\n"
                + string.Join("\n", report.Reasons.Concat(report.Warnings)) + $"\n降级：{report.RecommendedFallback}";
            Status = $"只读检查完成；诊断 ID：{result.CorrelationId}";
        }
        catch (OperationCanceledException) { Status = "操作已取消。"; }
        catch (Exception error) { HandleCommandFailure(error); }
        finally { IsBusy = false; OnPropertyChanged(nameof(LogStatus)); }
    }

    private void HandleCommandFailure(Exception error)
    {
        Guid id = Guid.NewGuid();
        _diagnostics.Record(new(DiagnosticEvent.CommandFailure, DiagnosticLevel.Error, id,
            DiagnosticCode.InternalError, ExceptionHResult: error.HResult));
        Status = FriendlyError(OperationCodes.InternalError, id.ToString("N"));
        ReportText = Status;
    }

    private static string FriendlyError(string code, string? id) =>
        $"{code switch
        {
            OperationCodes.WindowNotFound => "目标已退出或身份已变化，请刷新。",
            OperationCodes.WindowNotVisible => "目标已隐藏，请刷新。",
            OperationCodes.AccessDenied => "无法读取目标权限，不自动提权。",
            OperationCodes.Cancelled => "操作已取消。",
            _ => "操作失败，请使用诊断 ID 检查本地日志。"
        }} [{code}] ID: {id}";

    public async ValueTask DisposeAsync()
    {
        if (_stopping) return;
        _stopping = true;
        _lifetime.Cancel();
        RefreshCommand.Cancel();
        CheckCommand.Cancel();
        await Task.WhenAll(RefreshCommand.ExecutionTask ?? Task.CompletedTask, CheckCommand.ExecutionTask ?? Task.CompletedTask);
        _lifetime.Dispose();
    }
}
