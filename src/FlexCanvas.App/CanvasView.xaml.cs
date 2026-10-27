using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using FlexCanvas.Application;
using FlexCanvas.Domain;
using FlexCanvas.Infrastructure.Win32;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlexCanvas.App;

/// <summary>Empty canvas viewport; gestures are local to its background, never global input hooks.</summary>
public partial class CanvasView : UserControl
{
    private readonly CanvasViewModel _model = new();
    private Point? _dragStart;
    private double _startX, _startY;
    private bool _syncing;
    private readonly WindowOverviewModel _overview = new();
    private readonly Dictionary<Guid, Border> _cards = [];
    private readonly List<Line> _guides = [];
    private Guid? _moving;
    private Point _moveStart;
    private ContainerLayout? _moveOriginal;
    private IReadOnlyDictionary<nint, string> _windowNames = new Dictionary<nint, string>();
    private IReadOnlyDictionary<Guid, (int Width, int Height, uint Dpi)> _originalGeometry = new Dictionary<Guid, (int, int, uint)>();
    private IWindowEmbeddingService? _embedding;
    private bool _nativeMode;
    private IReadOnlyList<EmbeddingSessionStatus> _sessionsSnapshot = [];
    private readonly Dictionary<nint, NativeWindowHost> _hostsByHandle = [];
    private readonly Dictionary<Guid, NativeWindowHost> _nativeHosts = [];
    private readonly Dictionary<Guid, WindowBounds> _lastPlaced = [];
    private readonly Dictionary<Guid, WindowBounds> _pendingPlacements = [];
    private bool _placementQueued;

    /// <summary>Provides the embedding service used for 1:1 placements and shutdown detaches.</summary>
    public void AttachServices(IWindowEmbeddingService embedding) => _embedding = embedding;

    /// <summary>Creates a canvas host surface for a new embedding and returns its HWND; 0 when realization failed.</summary>
    public nint PrepareNativeHost()
    {
        var host = new NativeWindowHost { Width = 400, Height = 300, Visibility = Visibility.Hidden };
        NativeOverlay.Children.Add(host);
        UpdateLayout();
        if (host.SurfaceHandle == 0)
        {
            NativeOverlay.Children.Remove(host);
            return 0;
        }
        _hostsByHandle[host.SurfaceHandle] = host;
        return host.SurfaceHandle;
    }

    /// <summary>Detaches every canvas-hosted session; a false result keeps hosts for retry and blocks shutdown.</summary>
    public async Task<bool> DetachAllAsync()
    {
        if (_embedding is null || _nativeHosts.Count == 0) return true;
        var sessions = await _embedding.GetSessionsAsync();
        var byId = sessions.Where(item => item.NeedsRestore).ToDictionary(item => item.SessionId);
        bool all = true;
        foreach (var id in _nativeHosts.Keys.ToArray())
        {
            if (!byId.ContainsKey(id)) continue;
            var result = await _embedding.DetachAsync(id);
            if (!result.Success) all = false;
        }
        return all;
    }

    /// <summary>Switches between the geometry overview and the native 1:1 mode sharing one layout.</summary>
    public void SetNativeMode(bool native)
    {
        if (_nativeMode == native) return;
        _nativeMode = native;
        ModeToggle.Content = _nativeMode ? "模式：原生1:1" : "模式：总览";
        DrawCards();
        if (_nativeMode) PlaceVisibleHosts();
        else foreach (var host in _nativeHosts.Values) host.Visibility = Visibility.Hidden;
    }

    private void OnModeToggleClick(object sender, RoutedEventArgs e) => SetNativeMode(!_nativeMode);

    /// <summary>Refreshes geometry from real captured session bounds, preserving edited positions.</summary>
    public void UpdateSessions(IEnumerable<EmbeddingSessionStatus> sessions,
        IReadOnlyDictionary<nint, string>? windowNames = null)
    {
        _sessionsSnapshot = sessions.Where(item => item.NeedsRestore)
            .DistinctBy(item => item.SessionId).ToArray();
        _windowNames = windowNames ?? new Dictionary<nint, string>();
        _originalGeometry = _sessionsSnapshot.ToDictionary(item => item.SessionId,
                item => (item.Original.Bounds.Width, item.Original.Bounds.Height, item.Original.Dpi));
        _overview.Update(_sessionsSnapshot, status =>
            _windowNames.TryGetValue(status.Original.Identity.Hwnd, out var name) ? name : null);
        // Sync the shared layout with externally changed target sizes (read back by the engine).
        foreach (var session in _sessionsSnapshot)
        {
            if (session.ExternalBounds is not { } external) continue;
            _overview.Resize(session.SessionId,
                NativeGeometry.ToDip(external.Width, session.Original.Dpi),
                NativeGeometry.ToDip(external.Height, session.Original.Dpi));
        }
        DrawCards();
        UpdateOmissionStatus();
        SyncNativeHosts();
    }

    private void SyncNativeHosts()
    {
        if (_embedding is null) return;
        var active = _sessionsSnapshot.ToDictionary(item => item.SessionId);
        bool boundThisPass = false;
        // Bind newly prepared hosts to their session by host handle.
        foreach (var host in _hostsByHandle.Values.ToArray())
        {
            if (_nativeHosts.ContainsValue(host)) continue;
            var match = active.Values.FirstOrDefault(item => item.Host == host.SurfaceHandle);
            if (match is null) continue;
            _nativeHosts[match.SessionId] = host;
            boundThisPass = true;
        }
        // Drop hosts without a live session (embed failed, target exited or session detached).
        foreach (var entry in _hostsByHandle.Where(pair =>
                     !active.Values.Any(item => item.Host == pair.Key)).ToArray())
        {
            _hostsByHandle.Remove(entry.Key);
            if (NativeOverlay.Children.Contains(entry.Value)) NativeOverlay.Children.Remove(entry.Value);
        }
        foreach (var id in _nativeHosts.Where(pair => !active.ContainsKey(pair.Key)).Select(pair => pair.Key).ToArray())
        {
            _nativeHosts.Remove(id);
            _lastPlaced.Remove(id);
        }
        // Freshly bound sessions get their real projected bounds immediately, even in overview mode.
        if (_nativeMode || boundThisPass) PlaceVisibleHosts();
        else foreach (var host in _nativeHosts.Values) host.Visibility = Visibility.Hidden;
    }

    /// <summary>Positions canvas hosts from the shared layout. View transforms clip; the target size never changes.</summary>
    private void PlaceVisibleHosts()
    {
        if (_embedding is null) return;
        var region = _model.VisibleRegion();
        foreach (var (id, host) in _nativeHosts)
        {
            var container = _overview.Layout.Containers.FirstOrDefault(item => item.Id == id);
            if (container is null || !_sessionsSnapshot.Any(item => item.SessionId == id))
            { host.Visibility = Visibility.Hidden; continue; }
            var session = _sessionsSnapshot.First(item => item.SessionId == id);
            var placement = NativeGeometry.Project(container, session.Original.Dpi, region);
            if (!placement.IsVisible) { host.Visibility = Visibility.Hidden; continue; }
            double scale = session.Original.Dpi / 96d;
            // The host shows exactly the visible intersection; no WPF frame is drawn over the native window.
            double hostWidth = placement.HostSurface.Width / scale;
            double hostHeight = placement.HostSurface.Height / scale;
            // Anchor the host at the visible intersection origin so it never spills outside the viewport.
            double left = Math.Max(container.X, region.X) * _model.Zoom - _model.OffsetX;
            double top = Math.Max(container.Y, region.Y) * _model.Zoom - _model.OffsetY;
            host.Width = hostWidth; host.Height = hostHeight;
            Canvas.SetLeft(host, left);
            Canvas.SetTop(host, top);
            host.Visibility = _nativeMode ? Visibility.Visible : Visibility.Hidden;
            var target = new WindowBounds(-placement.TargetOffsetX, -placement.TargetOffsetY,
                placement.Target.Width, placement.Target.Height);
            if (_lastPlaced.TryGetValue(id, out var previous) && previous == target) continue;
            _lastPlaced[id] = target;
            QueuePlacement(id, target);
        }
        // Non-fullscreen focus highlight: reorder host Z order by layout layer; selected containers stay interactive,
        // neighbours keep their uncovered, clickable edges, and no session or saved layout is rebuilt.
        foreach (var entry in _nativeHosts
                     .OrderBy(pair => SelectedLayer(pair.Key))
                     .ThenBy(pair => pair.Value.SurfaceHandle))
            NativeWindowHost.MoveSurfaceToTop(entry.Value.SurfaceHandle);
    }

    private int SelectedLayer(Guid id)
    {
        var container = _overview.Layout.Containers.FirstOrDefault(item => item.Id == id);
        return container is null ? 0 : container.ZIndex;
    }

    /// <summary>Coalesces viewport/drag placement churn into one background dispatch, avoiding per-frame native churn.</summary>
    private void QueuePlacement(Guid id, WindowBounds bounds)
    {
        _pendingPlacements[id] = bounds;
        if (_placementQueued) return;
        _placementQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _placementQueued = false;
            foreach (var (sessionId, bounds2) in _pendingPlacements) _ = PlaceHostAsync(sessionId, bounds2);
            _pendingPlacements.Clear();
        });
    }

    private async Task PlaceHostAsync(Guid id, WindowBounds bounds)
    {
        try
        {
            var result = await _embedding!.PlaceAsync(id, bounds);
            if (!result.Success)
            {
                OverviewStatus.Text = $"原生放置失败（{result.Code} 0x{result.Win32ErrorCode:X}）；宿主保留，切换模式或刷新可重试。";
                OverviewStatus.Visibility = Visibility.Visible;
            }
        }
        catch (Exception error)
        {
            OverviewStatus.Text = $"原生放置异常（0x{error.HResult:X}）；宿主保留。";
            OverviewStatus.Visibility = Visibility.Visible;
        }
    }

    private void UpdateOmissionStatus()
    {
        if (_overview.Omissions.Count == 0) { OverviewStatus.Visibility = Visibility.Collapsed; return; }
        OverviewStatus.Text = string.Join("\n", _overview.Omissions.Select(omission =>
        {
            var name = _overview.Names.TryGetValue(omission.SessionId, out var resolved)
                ? resolved : $"会话 {omission.SessionId.ToString("N")[..8]}";
            return $"未纳入总览：{name} · 原始 {omission.PhysicalWidth}×{omission.PhysicalHeight} px @ {omission.Dpi} DPI"
                + $"（约 {omission.LogicalWidth:F0}×{omission.LogicalHeight:F0} DIP）——{omission.Reason}";
        }));
        OverviewStatus.Visibility = Visibility.Visible;
    }

    private void DrawCards()
    {
        foreach (var id in _cards.Keys.Where(id => !_overview.Layout.Containers.Any(item => item.Id == id)).ToArray())
        {
            LayoutSurface.Children.Remove(_cards[id]);
            _cards.Remove(id);
        }
        foreach (var item in _overview.Layout.Containers)
        {
            if (!_cards.TryGetValue(item.Id, out var card))
            {
                var text = new TextBlock { Margin = new Thickness(16), TextWrapping = TextWrapping.Wrap };
                // Overview-mode explicit resize handle; the shared layout resizes the native target with it.
                var grip = new Thumb
                {
                    Width = 12, Height = 12, Cursor = Cursors.SizeNWSE, Tag = item.Id,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                    Background = new SolidColorBrush(Color.FromArgb(0x88, 0x33, 0x55, 0x77))
                };
                grip.DragStarted += OnCardGripStarted;
                grip.DragDelta += OnCardGripDelta;
                grip.DragCompleted += OnCardGripCompleted;
                card = new Border { Background = Brushes.LightSteelBlue, BorderThickness = new(3), CornerRadius = new(8), Tag = item.Id,
                    Child = new Grid { Children = { text, grip } } };
                _cards.Add(item.Id, card);
                LayoutSurface.Children.Add(card);
                card.MouseLeftButtonDown += OnCardDown;
                card.MouseMove += OnCardMove;
                card.MouseLeftButtonUp += OnCardUp;
                card.LostMouseCapture += (_, _) => { _moving = null; ClearGuides(); };
            }
            card.Width = item.Width; card.Height = item.Height;
            card.BorderBrush = _overview.SelectedId == item.Id ? Brushes.DodgerBlue : Brushes.SlateGray;
            var name = _overview.Names.TryGetValue(item.Id, out var resolved)
                ? resolved : $"会话 {item.Id.ToString("N")[..8]}";
            var original = _originalGeometry.TryGetValue(item.Id, out var info)
                ? $"\n原始 {info.Width}×{info.Height} px @ {info.Dpi} DPI" : "";
            ((Grid)card.Child).Children.OfType<TextBlock>().Single().Text =
                $"{name}\n{item.Width:F0} x {item.Height:F0} DIP{original}\n布局矩形（非窗口内容）";
            Canvas.SetLeft(card, item.X); Canvas.SetTop(card, item.Y); Panel.SetZIndex(card, item.ZIndex + 1);
            // In native mode the real HWND replaces the rectangle for canvas-hosted sessions.
            card.Visibility = _nativeMode && _nativeHosts.ContainsKey(item.Id) ? Visibility.Hidden : Visibility.Visible;
        }
    }

    private void OnCardDown(object sender, MouseButtonEventArgs e)
    {
        var card = (Border)sender;
        Guid id = (Guid)card.Tag;
        _overview.Select(id);
        _moveOriginal = _overview.Layout.Containers.First(item => item.Id == id);
        _moveStart = e.GetPosition(LayoutSurface);
        if (!_moveOriginal.IsPinned && card.CaptureMouse()) _moving = id;
        DrawCards();
        e.Handled = true;
    }

    private void OnCardMove(object sender, MouseEventArgs e)
    {
        if (_moving is not { } id || _moveOriginal is not { } original || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(LayoutSurface);
        var result = _overview.Move(id, original.X + point.X - _moveStart.X, original.Y + point.Y - _moveStart.Y);
        DrawCards();
        ClearGuides();
        foreach (var guide in result.Guides)
        {
            var line = new Line { Stroke = Brushes.DodgerBlue, StrokeThickness = 2, IsHitTestVisible = false };
            line.X1 = guide.Vertical ? guide.Coordinate : guide.Start;
            line.X2 = guide.Vertical ? guide.Coordinate : guide.End;
            line.Y1 = guide.Vertical ? guide.Start : guide.Coordinate;
            line.Y2 = guide.Vertical ? guide.End : guide.Coordinate;
            Panel.SetZIndex(line, int.MaxValue);
            LayoutSurface.Children.Add(line); _guides.Add(line);
        }
        e.Handled = true;
    }

    private void OnCardUp(object sender, MouseButtonEventArgs e)
    {
        _moving = null;
        ((Border)sender).ReleaseMouseCapture();
        ClearGuides();
        e.Handled = true;
    }

    private Guid? _cardResizeId;
    private ContainerLayout? _cardResizeOriginal;

    private void OnCardGripStarted(object sender, DragStartedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not Guid id) return;
        var container = _overview.Layout.Containers.FirstOrDefault(item => item.Id == id);
        // Pinned or missing containers never start an explicit resize.
        if (container is null || container.IsPinned) return;
        _cardResizeId = id;
        _cardResizeOriginal = container;
    }

    private void OnCardGripDelta(object sender, DragDeltaEventArgs e)
    {
        if (_cardResizeId is not { } id || _cardResizeOriginal is not { } original) return;
        // Explicit resize through the shared layout is the sanctioned way the native target's real size changes.
        _overview.Resize(id, original.Width + e.HorizontalChange / _model.Zoom,
            original.Height + e.VerticalChange / _model.Zoom);
        DrawCards();
    }

    private void OnCardGripCompleted(object sender, DragCompletedEventArgs e)
    {
        _cardResizeId = null;
        _cardResizeOriginal = null;
    }

    private void ClearGuides()
    {
        foreach (var line in _guides) LayoutSurface.Children.Remove(line);
        _guides.Clear();
    }

    /// <summary>Creates the T0701 view without native windows or embedding sessions.</summary>
    public CanvasView()
    {
        InitializeComponent();
        DataContext = _model;
        Loaded += (_, _) => { _model.PropertyChanged += OnModelChanged; SyncViewport(); };
        Unloaded += (_, _) => { _model.PropertyChanged -= OnModelChanged; EndDrag(); };
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CanvasViewModel.Zoom) or nameof(CanvasViewModel.OffsetX) or nameof(CanvasViewModel.OffsetY))
        {
            SyncViewport();
            if (_nativeMode) PlaceVisibleHosts();
        }
    }

    private void SyncViewport()
    {
        if (_syncing) return;
        _syncing = true;
        try
        {
            Viewport.ScrollToHorizontalOffset(_model.OffsetX);
            Viewport.ScrollToVerticalOffset(_model.OffsetY);
        }
        finally { _syncing = false; }
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncing || e.OriginalSource != Viewport) return;
        _syncing = true;
        try
        {
            _model.SetViewport(Viewport.ViewportWidth, Viewport.ViewportHeight);
            // An extent change follows a zoom command; retain the model's logical origin.
            if (e.ExtentWidthChange == 0 && e.ExtentHeightChange == 0)
                _model.SetOffsets(Viewport.HorizontalOffset, Viewport.VerticalOffset);
        }
        finally { _syncing = false; }
        SyncViewport();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        if (!Viewport.CaptureMouse()) return;
        _dragStart = e.GetPosition(Viewport);
        _startX = _model.OffsetX;
        _startY = _model.OffsetY;
        Viewport.Cursor = Cursors.ScrollAll;
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not { } start) return;
        if (e.MiddleButton != MouseButtonState.Pressed) { EndDrag(); return; }
        Point current = e.GetPosition(Viewport);
        _model.SetOffsets(_startX + start.X - current.X, _startY + start.Y - current.Y);
        e.Handled = true;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || _dragStart is null) return;
        EndDrag();
        e.Handled = true;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e) => EndDrag();
    private void EndDrag()
    {
        _dragStart = null;
        Viewport.Cursor = null;
        if (Viewport.IsMouseCaptured) Viewport.ReleaseMouseCapture();
    }
}
