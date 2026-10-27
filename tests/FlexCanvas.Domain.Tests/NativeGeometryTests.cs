using Xunit;

namespace FlexCanvas.Domain.Tests;

public sealed class NativeGeometryTests
{
    private static readonly ContainerLayout Container =
        new(Guid.NewGuid(), 320, 240, 640, 480, 0);

    [Theory]
    [InlineData(800, 120, 640)]
    [InlineData(800, 96, 800)]
    [InlineData(800, 192, 400)]
    public void DpiConversionRoundTripsThroughPhysicalPixels(int physical, uint dpi, double expectedDip)
    {
        double dip = NativeGeometry.ToDip(physical, dpi);
        Assert.Equal(expectedDip, dip, 9);
        Assert.Equal(physical, NativeGeometry.ToPhysical(dip, dpi), 9);
    }

    [Fact]
    public void FullyVisibleContainerYieldsUnclippedHostAndZeroOffset()
    {
        var placement = NativeGeometry.Project(Container, 120, new ClipRegion(0, 0, 4096, 4096));
        // 640x480 DIP at 120 DPI is the original 800x600 physical size.
        Assert.Equal(800, placement.Target.Width);
        Assert.Equal(600, placement.Target.Height);
        Assert.Equal(800, placement.HostSurface.Width);
        Assert.Equal(600, placement.HostSurface.Height);
        Assert.Equal(0, placement.TargetOffsetX);
        Assert.Equal(0, placement.TargetOffsetY);
        Assert.True(placement.IsVisible);
    }

    [Fact]
    public void PartialLeftClipShowsSmallerHostButKeepsTargetSize()
    {
        // Viewport starts inside the container: only the right half is visible.
        var placement = NativeGeometry.Project(Container, 96, new ClipRegion(640, 240, 320, 480));
        Assert.Equal(640, placement.Target.Width);
        Assert.Equal(480, placement.Target.Height);
        Assert.Equal(320, placement.HostSurface.Width);
        Assert.Equal(480, placement.HostSurface.Height);
        Assert.Equal(320, placement.TargetOffsetX);
        Assert.Equal(0, placement.TargetOffsetY);
    }

    [Fact]
    public void PartialTopClipOffsetsTargetInsideTheHost()
    {
        var placement = NativeGeometry.Project(Container, 96, new ClipRegion(320, 480, 640, 240));
        Assert.Equal(240, placement.HostSurface.Height);
        Assert.Equal(0, placement.TargetOffsetX);
        Assert.Equal(240, placement.TargetOffsetY);
        Assert.Equal(640, placement.Target.Width);
        Assert.Equal(480, placement.Target.Height);
    }

    [Fact]
    public void DisjointRegionHidesTheHostWithoutResizingTheTarget()
    {
        var placement = NativeGeometry.Project(Container, 96, new ClipRegion(2000, 2000, 400, 400));
        Assert.False(placement.IsVisible);
        Assert.Equal(0, placement.HostSurface.Width);
        Assert.Equal(0, placement.HostSurface.Height);
        Assert.Equal(640, placement.Target.Width);
        Assert.Equal(480, placement.Target.Height);
    }

    [Fact]
    public void ViewTransformsNeverChangeTheProjectedTargetSize()
    {
        // 1:1 native mode: zoom and pan adjust the visible region only.
        var before = NativeGeometry.Project(Container, 144, new ClipRegion(0, 0, 1024, 1024));
        var zoomed = NativeGeometry.Project(Container, 144, new ClipRegion(256, 128, 512, 256));
        Assert.Equal(before.Target, zoomed.Target);
        Assert.NotEqual(before.HostSurface, zoomed.HostSurface);
    }

    [Fact]
    public void ZeroDpiAndNonFiniteInputsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeGeometry.ToPhysical(100, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeGeometry.ToDip(double.NaN, 96));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeGeometry.Project(Container, 0, new ClipRegion(0, 0, 10, 10)));
    }

    [Fact]
    public void ClippingIsExactAtDpiBoundaries()
    {
        // 100 DIP at 120 DPI is 125 physical pixels; 50 DIP is 62.5, rounded away from zero.
        var small = new ContainerLayout(Guid.NewGuid(), 0, 0, 100, 100, 0);
        var placement = NativeGeometry.Project(small, 120, new ClipRegion(50, 0, 100, 100));
        Assert.Equal(63, placement.HostSurface.Width);
        Assert.Equal(125, placement.HostSurface.Height);
        Assert.Equal(63, placement.TargetOffsetX);
        Assert.Equal(0, placement.TargetOffsetY);
    }
}
