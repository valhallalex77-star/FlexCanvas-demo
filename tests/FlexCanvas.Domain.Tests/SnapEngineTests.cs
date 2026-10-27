using Xunit;

namespace FlexCanvas.Domain.Tests;

public sealed class SnapEngineTests
{
    [Theory]
    [InlineData(331, 340)]
    [InlineData(328, 328)]
    [InlineData(320, 320)]
    public void AdjacencyUsesStrictThreshold(double proposedX, double expected)
    {
        var first = new ContainerLayout(Guid.NewGuid(), 100, 100, 240, 160, 0);
        var moving = new ContainerLayout(Guid.NewGuid(), proposedX, 500, 240, 160, 1);
        var result = SnapEngine.Move(new([first, moving]), moving);
        Assert.Equal(expected, result.Container.X);
    }

    [Fact]
    public void EqualGapAndAlignmentAreDeterministic()
    {
        var first = new ContainerLayout(Guid.NewGuid(), 0, 0, 240, 160, 0);
        var second = new ContainerLayout(Guid.NewGuid(), 260, 0, 240, 160, 1);
        var moving = new ContainerLayout(Guid.NewGuid(), 525, 5, 240, 160, 2);
        var layout = new CanvasLayout([first, second, moving]);
        var snapped = SnapEngine.Move(layout, moving);
        Assert.Equal(520, snapped.Container.X);
        Assert.Equal(0, snapped.Container.Y);
        Assert.Equal(2, snapped.Guides.Count);
        Assert.Equal(snapped.Container, SnapEngine.Move(layout, moving).Container);
    }

    [Fact]
    public void PinnedGeometryAndInvalidThresholdNeverSnap()
    {
        var first = new ContainerLayout(Guid.NewGuid(), 0, 0, 240, 160, 0);
        var moving = new ContainerLayout(Guid.NewGuid(), 245, 5, 240, 160, 1) { IsPinned = true };
        var layout = new CanvasLayout([first, moving]);
        Assert.Equal(moving, SnapEngine.Move(layout, moving).Container);
        Assert.Empty(SnapEngine.Move(layout, moving with { IsPinned = false }, double.NaN).Guides);
    }

    [Fact]
    public void BothAxesSnapSimultaneously()
    {
        var first = new ContainerLayout(Guid.NewGuid(), 0, 0, 240, 160, 0);
        var moving = new ContainerLayout(Guid.NewGuid(), 251, 6, 240, 160, 1);
        var result = SnapEngine.Move(new([first, moving]), moving);
        Assert.Equal(240, result.Container.X);   // right edge of first, 11 DIP inside the threshold
        Assert.Equal(0, result.Container.Y);    // top edge of first
        Assert.Equal(2, result.Guides.Count);
    }

    [Fact]
    public void DistanceExactlyAtThresholdIsNotSnapped()
    {
        var first = new ContainerLayout(Guid.NewGuid(), 0, 0, 240, 160, 0);
        var moving = new ContainerLayout(Guid.NewGuid(), 252, 500, 240, 160, 1);
        var result = SnapEngine.Move(new([first, moving]), moving, 12);
        Assert.Equal(252, result.Container.X);   // exactly 12 is not strictly inside the threshold
    }

    [Fact]
    public void SnapStaysWithinCanvasBoundsAndNeverMovesOtherTargets()
    {
        var pinned = new ContainerLayout(Guid.NewGuid(), 0, 0, 240, 160, 0) { IsPinned = true };
        var moving = new ContainerLayout(Guid.NewGuid(), 3, 500, 240, 160, 1);
        var layout = new CanvasLayout([pinned, moving]);
        var result = SnapEngine.Move(layout, moving);
        Assert.Equal(0, result.Container.X);     // snapped to the pinned edge at the canvas boundary
        Assert.Equal(0, pinned.X);
        Assert.Equal(240, pinned.Width);
    }

    [Fact]
    public void RepeatedRoundsNeverDriftGeometry()
    {
        var first = new ContainerLayout(Guid.NewGuid(), 0, 0, 240, 160, 0);
        var moving = new ContainerLayout(Guid.NewGuid(), 246, 500, 240, 160, 1);
        var layout = new CanvasLayout([first, moving]);
        var snapped = SnapEngine.Move(layout, moving).Container;
        for (int i = 0; i < 1000; i++)
        {
            var again = SnapEngine.Move(layout, snapped);
            Assert.Equal(snapped, again.Container);
            Assert.Equal(240, again.Container.X);
        }
    }
}
