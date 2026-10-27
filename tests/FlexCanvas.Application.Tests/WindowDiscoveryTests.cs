using FlexCanvas.Domain;
using Xunit;

namespace FlexCanvas.Application.Tests;

public sealed class WindowDiscoveryTests
{
    private static ExternalWindowInfo Window(nint hwnd = 100, uint pid = 42) =>
        new(new(hwnd, pid), "Same title", "Probe", "ProbeClass", new(-100, 20, 640, 480),
            0x10000000, 0, false, false, false, false, DpiMode.PerMonitorV2, 120);

    [Fact]
    public void PassingScreenIsNotACompatibilityGuarantee()
    {
        var report = CompatibilityPolicy.Evaluate(Window());
        Assert.True(report.CanAttempt);
        Assert.Equal(RiskLevel.Medium, report.RiskLevel);
        Assert.NotEmpty(report.Warnings);
        Assert.Contains("独立窗口", report.RecommendedFallback);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(false, null)]
    [InlineData(null, null)]
    public void UnknownPermissionIsNotInvented(bool? target, bool? host)
    {
        var report = CompatibilityPolicy.Evaluate(Window() with { IsElevated = target, HostIsElevated = host });
        Assert.False(report.CanAttempt);
        Assert.Equal(RiskLevel.Unknown, report.RiskLevel);
    }

    [Theory]
    [InlineData(DpiMode.Unknown)]
    [InlineData(DpiMode.Unaware)]
    [InlineData(DpiMode.SystemAware)]
    [InlineData(DpiMode.PerMonitor)]
    public void OtherDpiModesAreConservative(DpiMode mode) =>
        Assert.False(CompatibilityPolicy.Evaluate(Window() with { DpiMode = mode }).CanAttempt);

    [Fact]
    public void KnownUnsafeVariantsAreBlocked()
    {
        ExternalWindowInfo[] variants =
        [
            Window() with { IsElevated = true },
            Window() with { Style = 0x40000000 },
            Window() with { ExtendedStyle = 0x80 },
            Window() with { ExtendedStyle = 0x80000 },
            Window() with { ExtendedStyle = 0x08000000 },
            Window() with { HasOwner = true },
            Window() with { IsMinimized = true },
            Window() with { ClassName = "" },
            Window() with { Bounds = new(0, 0, 0, 1) },
            Window() with { Dpi = 0 }
        ];
        Assert.All(variants, w => Assert.False(CompatibilityPolicy.Evaluate(w).CanAttempt));
    }

    [Fact]
    public async Task CheckRereadsIdentityInsteadOfTrustingOldSnapshot()
    {
        var discovery = new FakeDiscovery { ReadResult = OperationResult<ExternalWindowInfo>.Fail(
            OperationCodes.WindowNotFound, "Gone", "correlation") };
        var result = await new WindowCompatibilityService(discovery).CheckAsync(Window().Identity);
        Assert.False(result.Success);
        Assert.Equal(Window().Identity, discovery.LastRead);
        Assert.Equal("correlation", result.CorrelationId);
    }

    [Fact]
    public async Task DuplicateTitlesSearchAndRepeatedRefreshPreserveExactIdentity()
    {
        var discovery = new FakeDiscovery { Items = [Window(), Window(101), Window(102, 43) with { ProcessName = "Other" }] };
        await using var vm = Create(discovery);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.Windows.Count);
        vm.SelectedWindow = vm.Windows[1];
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(new WindowIdentity(101, 42), vm.SelectedWindow!.Identity);
        Assert.Equal(3, vm.Windows.Count);
        vm.SearchText = "other";
        Assert.Single(vm.Windows);
        Assert.Null(vm.SelectedWindow);
        vm.SearchText = "no such window";
        Assert.Empty(vm.Windows);
        Assert.False(vm.HasWindows);
        vm.SearchText = "PID 42";
        Assert.Equal(2, vm.Windows.Count);
    }

    [Fact]
    public async Task ExitedSelectionIsRemovedWithoutChoosingAnotherSameTitleWindow()
    {
        var discovery = new FakeDiscovery { Items = [Window(), Window(101)] };
        await using var vm = Create(discovery);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedWindow = vm.Windows[0];
        await vm.CheckCommand.ExecuteAsync(null);
        Assert.Null(vm.SelectedWindow);
        Assert.Single(vm.Windows);
        Assert.Equal((nint)101, vm.Windows[0].Identity.Hwnd);
        Assert.False(vm.CheckCommand.CanExecute(null));
    }

    [Fact]
    public async Task FailedRefreshClearsStaleListAndHasCorrelatedError()
    {
        var discovery = new FakeDiscovery { Items = [Window()] };
        await using var vm = Create(discovery);
        await vm.RefreshCommand.ExecuteAsync(null);
        discovery.FailEnumeration = true;
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Empty(vm.Windows);
        Assert.Contains(OperationCodes.InternalError, vm.Status);
        Assert.Contains("enumeration-id", vm.Status);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task CommandExceptionDoesNotExposeSecretMessageOrLeaveBusyState()
    {
        var discovery = new FakeDiscovery { ThrowEnumeration = true };
        var sink = new FakeSink();
        await using var vm = Create(discovery, sink);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.DoesNotContain("password", vm.Status);
        Assert.False(vm.IsBusy);
        Assert.Single(sink.Entries);
        Assert.Equal(DiagnosticCode.InternalError, sink.Entries[0].Code);
        Assert.NotEqual(Guid.Empty, sink.Entries[0].CorrelationId);
    }

    [Fact]
    public async Task LateCheckCannotOverwriteAChangedSelection()
    {
        var discovery = new FakeDiscovery { Items = [Window(), Window(101)] };
        var check = new DelayedCheck();
        await using var vm = new WindowManagerViewModel(discovery, check, new FakeSink());
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedWindow = vm.Windows[0];
        Task pending = vm.CheckCommand.ExecuteAsync(null);
        vm.SelectedWindow = vm.Windows[1];
        check.Completion.SetResult(OperationResult<CompatibilityReport>.Ok(CompatibilityPolicy.Evaluate(Window()), "old-id"));
        await pending;
        Assert.DoesNotContain("old-id", vm.Status);
        Assert.Equal((nint)101, vm.SelectedWindow!.Identity.Hwnd);
        Assert.Contains("尚未检查", vm.ReportText);
    }

    private static WindowManagerViewModel Create(FakeDiscovery discovery, FakeSink? sink = null) =>
        new(discovery, new WindowCompatibilityService(discovery), sink ?? new());

    private sealed class FakeDiscovery : IWindowDiscoveryService
    {
        public IReadOnlyList<ExternalWindowInfo> Items { get; set; } = [];
        public bool FailEnumeration { get; set; }
        public bool ThrowEnumeration { get; set; }
        public WindowIdentity LastRead { get; private set; }
        public OperationResult<ExternalWindowInfo> ReadResult { get; set; } =
            OperationResult<ExternalWindowInfo>.Fail(OperationCodes.WindowNotFound, "Gone", "read-id");
        public Task<OperationResult<IReadOnlyList<ExternalWindowInfo>>> EnumerateAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowEnumeration) throw new InvalidOperationException("password and private path");
            return Task.FromResult(FailEnumeration
                ? OperationResult<IReadOnlyList<ExternalWindowInfo>>.Fail(OperationCodes.InternalError, "Failed", "enumeration-id")
                : OperationResult<IReadOnlyList<ExternalWindowInfo>>.Ok(Items, "enumeration-id"));
        }
        public Task<OperationResult<ExternalWindowInfo>> ReadAsync(WindowIdentity identity, CancellationToken cancellationToken = default)
        { LastRead = identity; return Task.FromResult(ReadResult); }
    }
    private sealed class DelayedCheck : IWindowCompatibilityService
    {
        public TaskCompletionSource<OperationResult<CompatibilityReport>> Completion { get; } = new();
        public Task<OperationResult<CompatibilityReport>> CheckAsync(WindowIdentity identity, CancellationToken cancellationToken = default) =>
            Completion.Task;
    }
    private sealed class FakeSink : IDiagnosticSink
    {
        public bool IsHealthy => true;
        public List<DiagnosticEntry> Entries { get; } = [];
        public void Record(DiagnosticEntry entry) => Entries.Add(entry);
    }
}
