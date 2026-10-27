using Xunit;

namespace FlexCanvas.Domain.Tests;

public sealed class LayoutEngineTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private readonly LayoutEngine _engine = new();
    private static CanvasLayout Layout() => new([
        new(A, 0, 0, 240, 160, 0),
        new(B, 100, 100, 640, 480, 10),
        new(C, 200, 200, 800, 600, int.MaxValue)]);

    [Fact]
    public void EmptyLayoutAndOverlappingContainersAreValid()
    {
        Assert.True(LayoutValidator.Validate(new CanvasLayout([])).Success);
        Assert.True(LayoutValidator.Validate(Layout()).Success);
    }

    [Fact]
    public void SnapshotCopiesInputAndCannotBeChangedThroughCollection()
    {
        var source = Layout().Containers.ToList();
        var layout = new CanvasLayout(source);
        source.Clear();
        Assert.Equal(3, layout.Containers.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<ContainerLayout>)layout.Containers).Clear());
    }

    [Fact]
    public void MoveAndResizeAreImmutableAndDeterministic()
    {
        var source = Layout();
        var first = _engine.MoveContainer(source, A, 1000, 2000);
        var second = _engine.MoveContainer(source, A, 1000, 2000);
        Assert.True(first.Success);
        Assert.Equal(first.Layout.Containers, second.Layout.Containers);
        Assert.Equal(0, source.Containers[0].X);
        var resized = _engine.ResizeContainer(first.Layout, A, 300, 400);
        Assert.True(resized.Success);
        Assert.Equal(300, resized.Layout.Containers[0].Width);
        Assert.Equal(240, first.Layout.Containers[0].Width);
        Assert.Equal(source.Containers[1], resized.Layout.Containers[1]);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    [InlineData(double.MaxValue, 0)]
    [InlineData(3857, 0)]
    [InlineData(0, 3937)]
    public void InvalidMovePreservesEntireSource(double x, double y)
    {
        var layout = Layout();
        var result = _engine.MoveContainer(layout, A, x, y);
        Assert.False(result.Success);
        Assert.Equal(OperationCodes.InvalidArgument, result.Code);
        Assert.Same(layout, result.Layout);
    }

    [Theory]
    [InlineData(239, 160)]
    [InlineData(240, 159)]
    [InlineData(double.NaN, 160)]
    [InlineData(240, double.NegativeInfinity)]
    [InlineData(double.MaxValue, 160)]
    [InlineData(4097, 160)]
    public void InvalidSizePreservesEntireSource(double width, double height)
    {
        var layout = Layout();
        var result = _engine.ResizeContainer(layout, A, width, height);
        Assert.False(result.Success);
        Assert.Same(layout, result.Layout);
    }

    [Fact]
    public void ExactCanvasEdgesAndFullCanvasSizeAreValid()
    {
        var layout = Layout();
        Assert.True(_engine.MoveContainer(layout, A, 3856, 3936).Success);
        Assert.True(_engine.ResizeContainer(layout, A, 4096, 4096).Success);
        var moved = _engine.MoveContainer(layout, A, 100, 100).Layout;
        Assert.False(_engine.ResizeContainer(moved, A, 4096, 4096).Success);
    }

    [Fact]
    public void InvalidIdsLayersAndNullEntriesAreRejected()
    {
        var layout = Layout();
        foreach (var invalid in new CanvasLayout[]
        {
            new([layout.Containers[0] with { Id = Guid.Empty }]),
            new([layout.Containers[0], layout.Containers[0] with { ZIndex = 2 }]),
            new([layout.Containers[0], layout.Containers[1] with { ZIndex = 0 }]),
            new([layout.Containers[0] with { ZIndex = -1 }]),
            new([null!])
        })
        {
            Assert.False(LayoutValidator.Validate(invalid).Success);
            Assert.Same(invalid, _engine.MoveContainer(invalid, A, 10, 10).Layout);
        }
    }

    [Fact]
    public void MissingTargetNeverChangesOtherContainers()
    {
        var layout = Layout();
        foreach (var result in new[]
        {
            _engine.MoveContainer(layout, Guid.Empty, 10, 10),
            _engine.ResizeContainer(layout, Guid.NewGuid(), 300, 200),
            _engine.BringToFront(layout, Guid.Empty),
            _engine.SendToBack(new CanvasLayout([]), A)
        }) Assert.False(result.Success);
        Assert.Same(layout, _engine.BringToFront(layout, Guid.Empty).Layout);
    }

    [Fact]
    public void LayerChangesPreserveRelativeOrderAndDoNotOverflow()
    {
        var source = Layout();
        var front = _engine.BringToFront(source, A);
        Assert.True(front.Success);
        Assert.Equal(new[] { B, C, A }, front.Layout.Containers.OrderBy(item => item.ZIndex).Select(item => item.Id));
        Assert.Equal(new[] { 0, 1, 2 }, front.Layout.Containers.Select(item => item.ZIndex).Order());
        var back = _engine.SendToBack(front.Layout, A);
        Assert.Equal(new[] { A, B, C }, back.Layout.Containers.OrderBy(item => item.ZIndex).Select(item => item.Id));
        Assert.Equal(int.MaxValue, source.Containers[2].ZIndex);
        Assert.Same(front.Layout, _engine.BringToFront(front.Layout, A).Layout);
        Assert.Same(back.Layout, _engine.SendToBack(back.Layout, A).Layout);
    }

    [Fact]
    public void RepeatedLayerAndPositionOperationsDoNotDriftGeometry()
    {
        var original = Layout();
        var current = original;
        for (int i = 0; i < 1000; i++)
        {
            current = _engine.BringToFront(current, A).Layout;
            current = _engine.SendToBack(current, A).Layout;
            current = _engine.MoveContainer(current, A, 0.125, 0.25).Layout;
            current = _engine.MoveContainer(current, A, 0, 0).Layout;
        }
        Assert.Equal(original.Containers.Select(item => (item.X, item.Y, item.Width, item.Height)),
            current.Containers.Select(item => (item.X, item.Y, item.Width, item.Height)));
    }

    [Fact]
    public void DomainLayoutHasNoPresentationOrInfrastructureReferences()
    {
        var references = typeof(CanvasLayout).Assembly.GetReferencedAssemblies().Select(item => item.Name).ToArray();
        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("FlexCanvas.Infrastructure", references);
        Assert.DoesNotContain("FlexCanvas.Application", references);
    }

    [Fact]
    public void PinLocksPositionAndSizeButAllowsLayers()
    {
        var original = Layout();
        var pinned = _engine.SetPinned(original, A, true).Layout;
        Assert.False(original.Containers[0].IsPinned);
        Assert.True(pinned.Containers[0].IsPinned);
        Assert.Same(pinned, _engine.MoveContainer(pinned, A, 10, 10).Layout);
        Assert.False(_engine.MoveContainer(pinned, A, 10, 10).Success);
        Assert.False(_engine.ResizeContainer(pinned, A, 500, 400).Success);
        Assert.True(_engine.BringToFront(pinned, A).Success);
        Assert.True(_engine.SendToBack(pinned, C).Success);
        Assert.True(_engine.BringToFront(pinned, A).Layout.Containers[0].IsPinned);
    }

    [Fact]
    public void PinAndUnpinAreIdempotentAndRestoreEditing()
    {
        var original = Layout();
        var pinned = _engine.SetPinned(original, A, true).Layout;
        Assert.Same(pinned, _engine.SetPinned(pinned, A, true).Layout);
        Assert.Same(pinned, _engine.MoveContainer(pinned, A, 0, 0).Layout);
        Assert.True(_engine.ResizeContainer(pinned, A, 240, 160).Success);
        var unlocked = _engine.SetPinned(pinned, A, false).Layout;
        Assert.True(_engine.MoveContainer(unlocked, A, 100, 100).Success);
        Assert.True(_engine.ResizeContainer(unlocked, A, 500, 400).Success);
        Assert.Equal(original.Containers, unlocked.Containers);
        Assert.False(_engine.SetPinned(original, Guid.Empty, true).Success);
    }
}
