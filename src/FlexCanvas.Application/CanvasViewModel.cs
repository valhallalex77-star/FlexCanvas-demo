using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexCanvas.Domain;

namespace FlexCanvas.Application;

/// <summary>Finite canvas viewport in DIP; independent of WPF and native window mutations.</summary>
public sealed class CanvasViewModel : ObservableObject
{
    /// <summary>Logical width and height of the confirmed positive-coordinate canvas.</summary>
    public const double CanvasExtent = 4096;
    /// <summary>Smallest supported layout zoom.</summary>
    public const double MinimumZoom = 0.5;
    /// <summary>Largest supported layout zoom.</summary>
    public const double MaximumZoom = 2;
    private double _zoom = 1;
    private double _offsetX, _offsetY, _viewportWidth, _viewportHeight;

    /// <summary>Creates viewport commands without starting discovery or embedding.</summary>
    public CanvasViewModel()
    {
        ZoomInCommand = new RelayCommand(() => SetZoom(Math.Round(Zoom + 0.1, 2)), () => Zoom < MaximumZoom);
        ZoomOutCommand = new RelayCommand(() => SetZoom(Math.Round(Zoom - 0.1, 2)), () => Zoom > MinimumZoom);
        ResetCommand = new RelayCommand(() => { SetZoom(1); SetOffsets(0, 0); });
    }

    /// <summary>Layout scale; native content will reflow rather than undergo pixel scaling.</summary>
    public double Zoom => _zoom;
    /// <summary>Scaled canvas extent used by the view's scroll area.</summary>
    public double ScaledExtent => CanvasExtent * Zoom;
    /// <summary>Horizontal viewport offset in display DIP.</summary>
    public double OffsetX => _offsetX;
    /// <summary>Vertical viewport offset in display DIP.</summary>
    public double OffsetY => _offsetY;
    /// <summary>Readable viewport status; no target titles or input text.</summary>
    public string ViewportStatus => $"{Zoom:P0} | X: {OffsetX / Zoom:F0}, Y: {OffsetY / Zoom:F0} DIP | 4096 x 4096 DIP";
    /// <summary>Increases layout zoom by ten percentage points.</summary>
    public IRelayCommand ZoomInCommand { get; }
    /// <summary>Decreases layout zoom by ten percentage points.</summary>
    public IRelayCommand ZoomOutCommand { get; }
    /// <summary>Returns to the origin and 100 percent zoom.</summary>
    public IRelayCommand ResetCommand { get; }

    /// <summary>Rejects nonfinite/out-of-range scales without changing viewport state.</summary>
    public bool SetZoom(double zoom)
    {
        if (!double.IsFinite(zoom) || zoom < MinimumZoom || zoom > MaximumZoom) return false;
        if (zoom == _zoom) return true;
        double ratio = zoom / _zoom;
        _zoom = zoom;
        OnPropertyChanged(nameof(Zoom));
        OnPropertyChanged(nameof(ScaledExtent));
        SetOffsets(_offsetX * ratio, _offsetY * ratio);
        OnPropertyChanged(nameof(ViewportStatus));
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        return true;
    }

    /// <summary>Records visible dimensions and clamps offsets to the finite scroll area.</summary>
    public bool SetViewport(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 0 || height < 0) return false;
        _viewportWidth = width;
        _viewportHeight = height;
        return SetOffsets(_offsetX, _offsetY);
    }

    /// <summary>Sets display-DIP offsets, clamped to the currently reachable scroll area.</summary>
    public bool SetOffsets(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return false;
        SetProperty(ref _offsetX, Math.Clamp(x, 0, Math.Max(0, ScaledExtent - _viewportWidth)), nameof(OffsetX));
        SetProperty(ref _offsetY, Math.Clamp(y, 0, Math.Max(0, ScaledExtent - _viewportHeight)), nameof(OffsetY));
        OnPropertyChanged(nameof(ViewportStatus));
        return true;
    }

    /// <summary>Visible logical-DIP region of the canvas; zoom and offsets are view-only transforms.</summary>
    public ClipRegion VisibleRegion() => new(OffsetX / Zoom, OffsetY / Zoom, _viewportWidth / Zoom, _viewportHeight / Zoom);
}
