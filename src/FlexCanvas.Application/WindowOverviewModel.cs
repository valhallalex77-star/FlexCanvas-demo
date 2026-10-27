using CommunityToolkit.Mvvm.ComponentModel;
using FlexCanvas.Domain;

namespace FlexCanvas.Application;

/// <summary>A real session left out of the overview geometry; original numbers are listed, never fabricated.</summary>
public sealed record OverviewOmission(Guid SessionId, nint Hwnd, int PhysicalWidth, int PhysicalHeight,
    uint Dpi, double LogicalWidth, double LogicalHeight, string Reason);

/// <summary>Shared logical layout for real sessions; view zoom never mutates native window dimensions.</summary>
public sealed class WindowOverviewModel : ObservableObject
{
    private CanvasLayout _layout = new([]);
    private Guid? _selected;
    private readonly LayoutEngine _engine = new();
    private IReadOnlyDictionary<Guid, string> _names = new Dictionary<Guid, string>();
    private IReadOnlyList<OverviewOmission> _omissions = [];

    /// <summary>Immutable logical-DIP layout, retained across refreshes for surviving sessions.</summary>
    public CanvasLayout Layout => _layout;
    /// <summary>Selected session identity; does not change native size.</summary>
    public Guid? SelectedId => _selected;
    /// <summary>Runtime display names resolved at import; not a persisted identity.</summary>
    public IReadOnlyDictionary<Guid, string> Names => _names;
    /// <summary>Sessions whose original geometry cannot be shown without fabricating logical sizes.</summary>
    public IReadOnlyList<OverviewOmission> Omissions => _omissions;
    /// <summary>Latest validation result suitable for presentation.</summary>
    public string Status { get; private set; } = "No real sessions.";

    /// <summary>Imports original physical bounds using each target's captured DPI, not a fixed host size.</summary>
    public void Update(IEnumerable<EmbeddingSessionStatus> sessions,
        Func<EmbeddingSessionStatus, string?>? nameResolver = null)
    {
        var active = sessions.Where(item => item.NeedsRestore).DistinctBy(item => item.SessionId).ToArray();
        var next = new List<ContainerLayout>();
        var names = new Dictionary<Guid, string>();
        var omissions = new List<OverviewOmission>();
        foreach (var session in active)
        {
            if (nameResolver?.Invoke(session) is { } name && name.Length > 0) names[session.SessionId] = name;
            var existing = _layout.Containers.FirstOrDefault(item => item.Id == session.SessionId);
            if (existing is not null) { next.Add(existing); continue; }
            var bounds = session.Original.Bounds;
            double scale = 96d / Math.Max(1, session.Original.Dpi);
            double width = bounds.Width * scale, height = bounds.Height * scale;
            if (width < CanvasLayout.MinimumWidth || height < CanvasLayout.MinimumHeight)
            {
                omissions.Add(new(session.SessionId, session.Original.Identity.Hwnd, bounds.Width, bounds.Height,
                    session.Original.Dpi, width, height,
                    $"Original {bounds.Width}x{bounds.Height}px at {session.Original.Dpi} DPI maps below the minimum logical size {CanvasLayout.MinimumWidth:F0}x{CanvasLayout.MinimumHeight:F0} DIP; not resized or fabricated."));
                continue;
            }
            if (width > CanvasLayout.Extent || height > CanvasLayout.Extent)
            {
                omissions.Add(new(session.SessionId, session.Original.Identity.Hwnd, bounds.Width, bounds.Height,
                    session.Original.Dpi, width, height,
                    $"Original {bounds.Width}x{bounds.Height}px at {session.Original.Dpi} DPI exceeds the {CanvasLayout.Extent:F0}x{CanvasLayout.Extent:F0} logical canvas; not resized or fabricated."));
                continue;
            }
            int z = next.Count == 0 ? 0 : next.Max(item => item.ZIndex) + 1;
            double x = Math.Min(120 + next.Count * 80, CanvasLayout.Extent - width);
            double y = Math.Min(120 + next.Count * 60, CanvasLayout.Extent - height);
            next.Add(new(session.SessionId, x, y, width, height, z));
        }
        // Removed sessions can leave sparse layer indices; normalize only the projections.
        var layers = next.OrderBy(item => item.ZIndex).Select((item, index) => (item.Id, index))
            .ToDictionary(item => item.Id, item => item.index);
        _layout = new(next.Select(item => item with { ZIndex = layers[item.Id] }));
        _names = names;
        _omissions = omissions.AsReadOnly();
        if (!next.Any(item => item.Id == _selected)) _selected = null;
        Status = next.Count == 0 && omissions.Count == 0
            ? "No real sessions."
            : $"{next.Count} session(s) shown; {omissions.Count} omitted for original geometry outside the logical canvas limits.";
        Notify();
    }

    /// <summary>Selects a surviving real session without changing its geometry or the native surface.</summary>
    public bool Select(Guid? id)
    {
        if (id is not null && !_layout.Containers.Any(item => item.Id == id)) return false;
        _selected = id;
        Notify();
        return true;
    }

    /// <summary>Commits logical position; a preview and its guides use the same deterministic snap calculation.</summary>
    public SnapResult Move(Guid id, double x, double y, bool snap = true)
    {
        var target = _layout.Containers.FirstOrDefault(item => item.Id == id);
        if (target is null) return new(new(id, 0, 0, 240, 160, 0), []);
        var proposed = target with
        {
            X = Math.Clamp(x, 0, CanvasLayout.Extent - target.Width),
            Y = Math.Clamp(y, 0, CanvasLayout.Extent - target.Height)
        };
        if (!double.IsFinite(x) || !double.IsFinite(y)) return new(target, []);
        var result = snap ? SnapEngine.Move(_layout, proposed) : new SnapResult(proposed, []);
        var changed = _engine.MoveContainer(_layout, id, result.Container.X, result.Container.Y);
        Status = changed.Message;
        if (changed.Success) _layout = changed.Layout;
        Notify();
        return changed.Success ? result : new(target, []);
    }

    /// <summary>Updates logical lock policy; native interaction integration must enforce this separately.</summary>
    public void SetPinned(Guid id, bool pinned)
    {
        var result = _engine.SetPinned(_layout, id, pinned);
        Status = result.Message;
        if (result.Success) _layout = result.Layout;
        Notify();
    }

    /// <summary>Resizes logical dimensions through the validated engine; pinned or invalid sizes keep the snapshot.</summary>
    public LayoutResult Resize(Guid id, double width, double height)
    {
        var result = _engine.ResizeContainer(_layout, id, width, height);
        Status = result.Message;
        if (result.Success) _layout = result.Layout;
        Notify();
        return result;
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(Layout));
        OnPropertyChanged(nameof(SelectedId));
        OnPropertyChanged(nameof(Names));
        OnPropertyChanged(nameof(Omissions));
        OnPropertyChanged(nameof(Status));
    }
}
