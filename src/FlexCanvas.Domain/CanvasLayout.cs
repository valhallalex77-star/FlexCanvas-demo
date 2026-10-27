namespace FlexCanvas.Domain;

/// <summary>Logical geometry only; contains neither HWND nor persistence identity.</summary>
public sealed record ContainerLayout(Guid Id, double X, double Y, double Width, double Height, int ZIndex)
{
    /// <summary>Locks logical position and dimensions, not selection, layer order or application input.</summary>
    public bool IsPinned { get; init; }
}

/// <summary>Immutable positive-coordinate layout. Input collections are defensively copied.</summary>
public sealed class CanvasLayout
{
    /// <summary>Confirmed canvas dimensions in logical DIP.</summary>
    public const double Extent = 4096;
    /// <summary>Baseline minimum logical container width, not a native tracking size guarantee.</summary>
    public const double MinimumWidth = 240;
    /// <summary>Baseline minimum logical container height.</summary>
    public const double MinimumHeight = 160;

    /// <summary>Copies supplied geometry; validation is explicit and does not silently repair input.</summary>
    public CanvasLayout(IEnumerable<ContainerLayout> containers)
    {
        ArgumentNullException.ThrowIfNull(containers);
        Containers = Array.AsReadOnly(containers.ToArray());
    }

    /// <summary>Read-only snapshot in input order; layer order is expressed by ZIndex.</summary>
    public IReadOnlyList<ContainerLayout> Containers { get; }
}

/// <summary>Operation failure retains the original immutable layout and an explanatory code.</summary>
public sealed record LayoutResult(bool Success, string Code, string Message, CanvasLayout Layout);

/// <summary>Pure finite canvas validation, independent of presentation and native APIs.</summary>
public static class LayoutValidator
{
    /// <summary>Rejects invalid geometry, empty/duplicate identifiers and ambiguous layers; overlap is allowed.</summary>
    public static LayoutResult Validate(CanvasLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var ids = new HashSet<Guid>();
        var layers = new HashSet<int>();
        foreach (var item in layout.Containers)
        {
            if (item is null || item.Id == Guid.Empty || !ids.Add(item.Id))
                return Fail(layout, "Container identifiers must be nonempty and unique.");
            if (item.ZIndex < 0 || !layers.Add(item.ZIndex))
                return Fail(layout, "Layer indices must be nonnegative and unique.");
            if (!double.IsFinite(item.X) || !double.IsFinite(item.Y)
                || !double.IsFinite(item.Width) || !double.IsFinite(item.Height)
                || item.X < 0 || item.Y < 0 || item.Width < CanvasLayout.MinimumWidth
                || item.Height < CanvasLayout.MinimumHeight
                || item.Width > CanvasLayout.Extent || item.Height > CanvasLayout.Extent
                || item.X > CanvasLayout.Extent - item.Width || item.Y > CanvasLayout.Extent - item.Height)
                return Fail(layout, "Container geometry must fit the finite canvas and minimum logical size.");
        }
        return new(true, OperationCodes.Ok, "Valid layout.", layout);
    }

    private static LayoutResult Fail(CanvasLayout layout, string message) =>
        new(false, OperationCodes.InvalidArgument, message, layout);
}

/// <summary>Pure immutable layout operations with geometry locking; snapping is implemented separately.</summary>
public sealed class LayoutEngine
{
    /// <summary>Moves a container in logical DIP; invalid coordinates preserve the source layout.</summary>
    public LayoutResult MoveContainer(CanvasLayout layout, Guid id, double x, double y) =>
        Change(layout, id, item => item with { X = x, Y = y });

    /// <summary>Changes logical dimensions, without scaling application pixels or invoking native operations.</summary>
    public LayoutResult ResizeContainer(CanvasLayout layout, Guid id, double width, double height) =>
        Change(layout, id, item => item with { Width = width, Height = height });

    /// <summary>Moves a container to the front; relative order of other containers is preserved.</summary>
    public LayoutResult BringToFront(CanvasLayout layout, Guid id) => Reorder(layout, id, true);

    /// <summary>Moves a container to the back; compact layers avoid integer overflow on repeated operations.</summary>
    public LayoutResult SendToBack(CanvasLayout layout, Guid id) => Reorder(layout, id, false);

    /// <summary>Locks or unlocks logical position and dimensions without affecting layer order.</summary>
    public LayoutResult SetPinned(CanvasLayout layout, Guid id, bool pinned)
    {
        var valid = LayoutValidator.Validate(layout);
        if (!valid.Success) return valid;
        var target = layout.Containers.FirstOrDefault(item => item.Id == id);
        if (target is null) return Missing(layout);
        if (target.IsPinned == pinned) return new(true, OperationCodes.Ok, "Lock state unchanged.", layout);
        return new(true, OperationCodes.Ok, "Geometry lock updated.",
            new CanvasLayout(layout.Containers.Select(item => item.Id == id ? item with { IsPinned = pinned } : item)));
    }

    private static LayoutResult Change(CanvasLayout layout, Guid id, Func<ContainerLayout, ContainerLayout> change)
    {
        var valid = LayoutValidator.Validate(layout);
        if (!valid.Success) return valid;
        var target = layout.Containers.FirstOrDefault(item => item.Id == id);
        if (target is null) return Missing(layout);
        var changed = change(target);
        if (target.IsPinned)
        {
            if (changed == target) return new(true, OperationCodes.Ok, "Geometry unchanged.", layout);
            return new(false, OperationCodes.InvalidArgument, "Container position and dimensions are locked; unlock before changing geometry.", layout);
        }
        var next = new CanvasLayout(layout.Containers.Select(item => item.Id == id ? changed : item));
        var result = LayoutValidator.Validate(next);
        return result.Success ? result : result with { Layout = layout };
    }

    private static LayoutResult Reorder(CanvasLayout layout, Guid id, bool front)
    {
        var valid = LayoutValidator.Validate(layout);
        if (!valid.Success) return valid;
        var target = layout.Containers.FirstOrDefault(item => item.Id == id);
        if (target is null) return Missing(layout);
        var ordered = layout.Containers.OrderBy(item => item.ZIndex).ToList();
        if ((front ? ordered[^1] : ordered[0]).Id == id)
            return new(true, OperationCodes.Ok, "Layer already at requested edge.", layout);
        ordered.Remove(target);
        if (front) ordered.Add(target);
        else ordered.Insert(0, target);
        var indices = ordered.Select((item, index) => (item.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index);
        return new(true, OperationCodes.Ok, "Layer order updated.",
            new CanvasLayout(layout.Containers.Select(item => item with { ZIndex = indices[item.Id] })));
    }

    private static LayoutResult Missing(CanvasLayout layout) =>
        new(false, OperationCodes.InvalidArgument, "Container not present in layout.", layout);
}
