using CommunityToolkit.Mvvm.ComponentModel;
using FlexCanvas.Domain;

namespace FlexCanvas.Application;

/// <summary>Selection of real embedding sessions, not placeholder canvas cards.</summary>
public sealed class ContainerSelectionModel : ObservableObject
{
    private IReadOnlyList<EmbeddingSessionStatus> _sessions = [];
    private Guid? _selectedId;

    /// <summary>Real sessions still requiring recovery, including failed restoration.</summary>
    public IReadOnlyList<EmbeddingSessionStatus> Sessions => _sessions;
    /// <summary>Session identity, independent of possibly repeated process identifiers.</summary>
    public Guid? SelectedId => _selectedId;
    /// <summary>Currently selected real session, or null when absent/terminal.</summary>
    public EmbeddingSessionStatus? Selected => _sessions.FirstOrDefault(item => item.SessionId == _selectedId);

    /// <summary>Replaces immutable projections and clears stale selection without silently selecting a different target.</summary>
    public void Update(IEnumerable<EmbeddingSessionStatus> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        _sessions = Array.AsReadOnly(sessions.Where(item => item.NeedsRestore)
            .DistinctBy(item => item.SessionId).ToArray());
        if (!_sessions.Any(item => item.SessionId == _selectedId)) _selectedId = null;
        OnPropertyChanged(nameof(Sessions));
        OnPropertyChanged(nameof(SelectedId));
        OnPropertyChanged(nameof(Selected));
    }

    /// <summary>Selects by session identity; an invalid request preserves the current selection.</summary>
    public bool Select(Guid? id)
    {
        if (id is not null && !_sessions.Any(item => item.SessionId == id)) return false;
        _selectedId = id;
        OnPropertyChanged(nameof(SelectedId));
        OnPropertyChanged(nameof(Selected));
        return true;
    }
}
