using FlexCanvas.Domain;
using Xunit;

namespace FlexCanvas.Application.Tests;

public sealed class ContainerSelectionTests
{
    private static EmbeddingSessionStatus Session(Guid id, bool restore = true) =>
        new(id, 500, new(new(100, 42), 123, 0, 0, 0, new(0, 0, 640, 480),
            default, 120), restore ? WindowEmbeddingState.Embedded : WindowEmbeddingState.Detached,
            restore, NativeWindowResult.Ok());

    [Fact]
    public void SelectsBySessionNotRepeatedPidAndPreservesOnRefresh()
    {
        var first = Session(Guid.NewGuid());
        var second = Session(Guid.NewGuid());
        var model = new ContainerSelectionModel();
        model.Update([first, second]);
        Assert.Null(model.Selected);
        Assert.True(model.Select(second.SessionId));
        model.Update([second, first]);
        Assert.Equal(second.SessionId, model.SelectedId);
        Assert.Equal(second, model.Selected);
        Assert.False(model.Select(Guid.NewGuid()));
        Assert.Equal(second.SessionId, model.SelectedId);
    }

    [Fact]
    public void TerminalOrAbsentSelectionIsClearedAndFailedRecoveryRemainsSelectable()
    {
        var session = Session(Guid.NewGuid());
        var model = new ContainerSelectionModel();
        model.Update([session with { State = WindowEmbeddingState.DetachFailed }]);
        Assert.True(model.Select(session.SessionId));
        Assert.Equal(WindowEmbeddingState.DetachFailed, model.Selected!.State);
        model.Update([Session(session.SessionId, false)]);
        Assert.Empty(model.Sessions);
        Assert.Null(model.Selected);
        model.Update([session]);
        model.Select(session.SessionId);
        model.Update([]);
        Assert.Null(model.SelectedId);
    }

    [Fact]
    public void DuplicateIdsAreDeduplicatedAndExplicitClearDoesNotMutateSessions()
    {
        var session = Session(Guid.NewGuid());
        var model = new ContainerSelectionModel();
        model.Update([session, session]);
        Assert.Single(model.Sessions);
        model.Select(session.SessionId);
        Assert.True(model.Select(null));
        Assert.Null(model.Selected);
        Assert.Single(model.Sessions);
    }
}
