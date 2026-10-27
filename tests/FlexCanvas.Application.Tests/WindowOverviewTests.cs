using FlexCanvas.Domain;
using Xunit;

namespace FlexCanvas.Application.Tests;

public sealed class WindowOverviewTests
{
    [Fact]
    public void OriginalPhysicalSizeMapsToDipAndPositionSurvivesRefresh()
    {
        var id = Guid.NewGuid();
        var session = new EmbeddingSessionStatus(id, 500,
            new(new(100, 42), 1, 0, 0, 0, new(0, 0, 800, 600), default, 120),
            WindowEmbeddingState.Embedded, true, NativeWindowResult.Ok());
        var model = new WindowOverviewModel();
        model.Update([session]);
        Assert.Equal(640, model.Layout.Containers[0].Width);
        Assert.Equal(480, model.Layout.Containers[0].Height);
        model.Move(id, 1000, 1000, false);
        model.Update([session]);
        Assert.Equal(1000, model.Layout.Containers[0].X);
        Assert.Equal(800, session.Original.Bounds.Width);
        model.Select(id);
        model.Update([]);
        Assert.Null(model.SelectedId);
    }

    [Fact]
    public void PinPreventsOverviewMoveWithoutChangingOriginalSnapshot()
    {
        var id = Guid.NewGuid();
        var session = new EmbeddingSessionStatus(id, 500,
            new(new(100, 42), 1, 0, 0, 0, new(0, 0, 800, 600), default, 120),
            WindowEmbeddingState.Embedded, true, NativeWindowResult.Ok());
        var model = new WindowOverviewModel();
        model.Update([session]);
        model.SetPinned(id, true);
        var before = model.Layout.Containers[0];
        model.Move(id, 1000, 1000);
        Assert.Equal(before, model.Layout.Containers[0]);
    }

    [Fact]
    public void ResizeUpdatesLogicalSizeAndRejectsPinnedOrInvalidValues()
    {
        var id = Guid.NewGuid();
        var session = SessionWithBounds(id, 800, 600, 120);
        var model = new WindowOverviewModel();
        model.Update([session]);
        var result = model.Resize(id, 960, 720);
        Assert.True(result.Success);
        Assert.Equal(960, model.Layout.Containers[0].Width);
        Assert.Equal(720, model.Layout.Containers[0].Height);
        Assert.False(model.Resize(id, 100, 50).Success);          // below the minimum logical size
        Assert.Equal(960, model.Layout.Containers[0].Width);
        Assert.False(model.Resize(Guid.NewGuid(), 800, 600).Success); // unknown session keeps the layout
        model.SetPinned(id, true);
        Assert.False(model.Resize(id, 500, 400).Success);          // pinned geometry is locked
        Assert.Equal(960, model.Layout.Containers[0].Width);
    }

    private static EmbeddingSessionStatus SessionWithBounds(Guid id, int width, int height, uint dpi) =>
        new(id, 500, new(new(100, 42), 1, 0, 0, 0, new(0, 0, width, height), default, dpi),
            WindowEmbeddingState.Embedded, true, NativeWindowResult.Ok());

    [Fact]
    public void ResolverNamesAreExposedWithoutChangingGeometry()
    {
        var id = Guid.NewGuid();
        var model = new WindowOverviewModel();
        model.Update([SessionWithBounds(id, 800, 600, 120)],
            status => $"Probe {status.Original.Identity.ProcessId}");
        var container = Assert.Single(model.Layout.Containers);
        Assert.Equal(id, container.Id);
        Assert.Equal(640, container.Width);
        Assert.Equal("Probe 42", Assert.Contains(id, model.Names));
        Assert.Empty(model.Omissions);
    }

    [Fact]
    public void TooSmallOriginalGeometryIsExplicitlyOmittedNotFabricated()
    {
        var id = Guid.NewGuid();
        var model = new WindowOverviewModel();
        model.Update([SessionWithBounds(id, 200, 100, 120)], _ => "Tiny Probe");
        Assert.Empty(model.Layout.Containers);
        var omission = Assert.Single(model.Omissions);
        Assert.Equal(id, omission.SessionId);
        Assert.Equal(200, omission.PhysicalWidth);
        Assert.Equal(100, omission.PhysicalHeight);
        Assert.Equal(120u, omission.Dpi);
        Assert.Equal(160, omission.LogicalWidth);
        Assert.Equal(80, omission.LogicalHeight);
        Assert.Contains("minimum", omission.Reason);
        Assert.Equal("Tiny Probe", Assert.Contains(id, model.Names));
    }

    [Fact]
    public void OversizeOriginalGeometryIsOmittedAndOmissionsClearWithSessions()
    {
        var id = Guid.NewGuid();
        var model = new WindowOverviewModel();
        model.Update([SessionWithBounds(id, 6000, 3000, 96)]);
        Assert.Empty(model.Layout.Containers);
        var omission = Assert.Single(model.Omissions);
        Assert.Equal(6000, omission.PhysicalWidth);
        Assert.Equal(6000, omission.LogicalWidth);
        Assert.Contains("exceeds", omission.Reason);
        model.Update([]);
        Assert.Empty(model.Omissions);
        Assert.Empty(model.Names);
    }

    [Fact]
    public void MixedFittingAndOmittedSessionsAreBothReported()
    {
        var fitting = Guid.NewGuid();
        var small = Guid.NewGuid();
        var model = new WindowOverviewModel();
        model.Update([SessionWithBounds(fitting, 800, 600, 120), SessionWithBounds(small, 100, 50, 96)]);
        Assert.Single(model.Layout.Containers);
        Assert.Equal(fitting, model.Layout.Containers[0].Id);
        var omission = Assert.Single(model.Omissions);
        Assert.Equal(small, omission.SessionId);
        Assert.Empty(model.Names); // no resolver supplied: names stay empty rather than invented
        Assert.Contains("omitted", model.Status);
    }
}
