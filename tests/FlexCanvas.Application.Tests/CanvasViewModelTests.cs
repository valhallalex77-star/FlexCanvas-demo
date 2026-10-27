using FlexCanvas.Application;
using Xunit;

namespace FlexCanvas.Application.Tests;

public sealed class CanvasViewModelTests
{
    [Fact]
    public void StartsAtOriginWithoutEmbedding()
    {
        var model = new CanvasViewModel();
        Assert.Equal(1, model.Zoom);
        Assert.Equal(4096, model.ScaledExtent);
        Assert.Equal(0, model.OffsetX);
        Assert.Equal(0, model.OffsetY);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0.49)]
    [InlineData(2.01)]
    public void InvalidZoomDoesNotChangeState(double zoom)
    {
        var model = new CanvasViewModel();
        model.SetOffsets(100, 200);
        Assert.False(model.SetZoom(zoom));
        Assert.Equal(1, model.Zoom);
        Assert.Equal(100, model.OffsetX);
        Assert.Equal(200, model.OffsetY);
    }

    [Fact]
    public void ZoomPreservesLogicalOriginAndClampsAtCanvasEdge()
    {
        var model = new CanvasViewModel();
        model.SetViewport(1000, 800);
        model.SetOffsets(100, 200);
        Assert.True(model.SetZoom(2));
        Assert.Equal(200, model.OffsetX);
        Assert.Equal(400, model.OffsetY);
        model.SetOffsets(10000, 10000);
        Assert.True(model.SetZoom(0.5));
        Assert.Equal(1048, model.OffsetX);
        Assert.Equal(1248, model.OffsetY);
    }

    [Fact]
    public void OffsetsAndViewportAreFiniteAndClamped()
    {
        var model = new CanvasViewModel();
        model.SetViewport(800, 600);
        Assert.True(model.SetOffsets(-10, 99999));
        Assert.Equal(0, model.OffsetX);
        Assert.Equal(3496, model.OffsetY);
        Assert.False(model.SetOffsets(double.NaN, 1));
        Assert.False(model.SetViewport(-1, 10));
        Assert.False(model.SetViewport(1, double.PositiveInfinity));
        Assert.Equal(3496, model.OffsetY);
        model.SetViewport(5000, 5000);
        Assert.Equal(0, model.OffsetY);
    }

    [Fact]
    public void CommandsRespectEndpointsAndResetOrigin()
    {
        var model = new CanvasViewModel();
        for (int i = 0; i < 10; i++) model.ZoomInCommand.Execute(null);
        Assert.Equal(2, model.Zoom);
        Assert.False(model.ZoomInCommand.CanExecute(null));
        for (int i = 0; i < 15; i++) model.ZoomOutCommand.Execute(null);
        Assert.Equal(0.5, model.Zoom);
        Assert.False(model.ZoomOutCommand.CanExecute(null));
        model.SetOffsets(300, 400);
        model.ResetCommand.Execute(null);
        Assert.Equal(1, model.Zoom);
        Assert.Equal(0, model.OffsetX);
        Assert.Equal(0, model.OffsetY);
    }

    [Fact]
    public void ViewportPropertiesRaiseNotifications()
    {
        var model = new CanvasViewModel();
        var changed = new List<string?>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        model.SetZoom(1.5);
        model.SetOffsets(100, 200);
        Assert.Contains(nameof(CanvasViewModel.Zoom), changed);
        Assert.Contains(nameof(CanvasViewModel.ScaledExtent), changed);
        Assert.Contains(nameof(CanvasViewModel.OffsetX), changed);
        Assert.Contains(nameof(CanvasViewModel.ViewportStatus), changed);
    }

    [Fact]
    public void VisibleRegionTracksViewTransformsInLogicalDip()
    {
        var model = new CanvasViewModel();
        model.SetViewport(1000, 800);
        model.SetOffsets(200, 400);
        var region = model.VisibleRegion();
        Assert.Equal(200, region.X);
        Assert.Equal(400, region.Y);
        Assert.Equal(1000, region.Width);
        Assert.Equal(800, region.Height);
        Assert.True(model.SetZoom(2));
        region = model.VisibleRegion();
        Assert.Equal(200, region.X);  // offsets double with zoom, so the logical origin is preserved
        Assert.Equal(400, region.Y);
        Assert.Equal(500, region.Width);
        Assert.Equal(400, region.Height);
    }
}
