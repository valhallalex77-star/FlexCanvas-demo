using FlexCanvas.Domain;

namespace FlexCanvas.Application;

public interface IDiagnosticSink
{
    bool IsHealthy { get; }
    void Record(DiagnosticEntry entry);
}

public interface IWindowDiscoveryService
{
    Task<OperationResult<IReadOnlyList<ExternalWindowInfo>>> EnumerateAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<ExternalWindowInfo>> ReadAsync(WindowIdentity identity, CancellationToken cancellationToken = default);
}

public interface IWindowCompatibilityService
{
    Task<OperationResult<CompatibilityReport>> CheckAsync(WindowIdentity identity, CancellationToken cancellationToken = default);
}

public static class CompatibilityPolicy
{
    public static CompatibilityReport Evaluate(ExternalWindowInfo window)
    {
        var reasons = new List<string>();
        var warnings = new List<string> { "初筛不是兼容保证；真实输入、IME、弹窗和恢复仍需验证。" };
        bool unknown = window.IsElevated is null || window.HostIsElevated is null || window.DpiMode == DpiMode.Unknown || window.Dpi == 0;
        if (window.IsElevated is null || window.HostIsElevated is null) reasons.Add("无法确认权限；不自动提权。");
        else if (window.IsElevated != window.HostIsElevated) reasons.Add("权限等级不匹配，可能受 UIPI 限制。");
        if (window.DpiMode == DpiMode.Unknown || window.Dpi == 0) reasons.Add("DPI 信息未知。");
        else if (window.DpiMode != DpiMode.PerMonitorV2) reasons.Add("DPI awareness 与 PerMonitorV2 宿主不一致。");
        if ((window.Style & 0x40000000L) != 0 || (window.ExtendedStyle & 0x80L) != 0)
            reasons.Add("子窗口或工具窗口不属于支持的候选类型。");
        if ((window.ExtendedStyle & (0x80000L | 0x08000000L)) != 0)
            reasons.Add("分层或不可激活窗口存在输入/绘制风险。");
        if (window.HasOwner) reasons.Add("拥有 owner 的窗口可能是对话框，保持独立运行。");
        if (window.IsMinimized) reasons.Add("请先恢复窗口再重新检查。");
        if (window.Bounds.Width <= 0 || window.Bounds.Height <= 0) reasons.Add("窗口矩形无效。");
        if (window.ClassName.Length == 0) reasons.Add("窗口类型未知。");
        warnings.Add("相同 elevation 不等于完整权限兼容，也无法排除自绘/GPU/airspace 风险。");
        return new(reasons.Count == 0, unknown ? RiskLevel.Unknown : reasons.Count > 0 ? RiskLevel.High : RiskLevel.Medium,
            reasons, warnings, "不兼容或未确认时保持外部应用为独立窗口；嵌入须再次确认。");
    }
}

public sealed class WindowCompatibilityService(IWindowDiscoveryService discovery) : IWindowCompatibilityService
{
    public async Task<OperationResult<CompatibilityReport>> CheckAsync(WindowIdentity identity, CancellationToken cancellationToken = default)
    {
        var result = await discovery.ReadAsync(identity, cancellationToken).ConfigureAwait(false);
        return result.Success && result.Data is not null
            ? OperationResult<CompatibilityReport>.Ok(CompatibilityPolicy.Evaluate(result.Data), result.CorrelationId!)
            : OperationResult<CompatibilityReport>.Fail(result.Code, result.Message, result.CorrelationId!);
    }
}
