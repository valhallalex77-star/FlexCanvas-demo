namespace FlexCanvas.Domain;

/// <summary>A visible logical-DIP rectangle of the finite canvas under the current viewport.</summary>
public sealed record ClipRegion(double X, double Y, double Width, double Height)
{
    /// <summary>True when the region covers no positive area.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>Integer physical pixel geometry for a native surface.</summary>
public sealed record NativeSurfaceBounds(int X, int Y, int Width, int Height);

/// <summary>Native 1:1 placement separating the target's real size from the visible host clip.</summary>
public sealed record NativePlacement(NativeSurfaceBounds Target, NativeSurfaceBounds HostSurface,
    int TargetOffsetX, int TargetOffsetY)
{
    /// <summary>True when at least part of the container intersects the visible region.</summary>
    public bool IsVisible => HostSurface.Width > 0 && HostSurface.Height > 0;
}

/// <summary>Pure projection between canvas DIP and per-window physical pixels; view transforms never resize the target.</summary>
public static class NativeGeometry
{
    /// <summary>Baseline logical DPI used by the canvas coordinate system.</summary>
    public const double LogicalDpi = 96;

    /// <summary>Converts logical DIP to physical pixels using the target's captured DPI.</summary>
    public static double ToPhysical(double dip, uint dpi)
    {
        if (!double.IsFinite(dip)) throw new ArgumentOutOfRangeException(nameof(dip), "DIP must be finite.");
        if (dpi == 0) throw new ArgumentOutOfRangeException(nameof(dpi), "DPI must be positive.");
        return dip * dpi / LogicalDpi;
    }

    /// <summary>Converts physical pixels to logical DIP using the target's captured DPI.</summary>
    public static double ToDip(double physical, uint dpi)
    {
        if (!double.IsFinite(physical)) throw new ArgumentOutOfRangeException(nameof(physical), "Pixel value must be finite.");
        if (dpi == 0) throw new ArgumentOutOfRangeException(nameof(dpi), "DPI must be positive.");
        return physical * LogicalDpi / dpi;
    }

    /// <summary>Projects a container into 1:1 native pixel geometry. Zoom/pan only clip; the target keeps its real size.</summary>
    public static NativePlacement Project(ContainerLayout container, uint dpi, ClipRegion visible)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(visible);
        if (dpi == 0) throw new ArgumentOutOfRangeException(nameof(dpi), "DPI must be positive.");
        // The target's real physical size depends only on its logical geometry and captured DPI.
        var target = new NativeSurfaceBounds(
            Round(ToPhysical(container.X, dpi)), Round(ToPhysical(container.Y, dpi)),
            Round(ToPhysical(container.Width, dpi)), Round(ToPhysical(container.Height, dpi)));
        double left = Math.Max(container.X, visible.X);
        double top = Math.Max(container.Y, visible.Y);
        double right = Math.Min(container.X + container.Width, visible.X + visible.Width);
        double bottom = Math.Min(container.Y + container.Height, visible.Y + visible.Height);
        if (right <= left || bottom <= top)
            return new(target, new NativeSurfaceBounds(0, 0, 0, 0), 0, 0);
        // The host surface shows only the visible intersection; the target is offset, never scaled.
        var host = new NativeSurfaceBounds(0, 0,
            Round(ToPhysical(right - left, dpi)), Round(ToPhysical(bottom - top, dpi)));
        return new(target, host,
            Round(ToPhysical(left - container.X, dpi)), Round(ToPhysical(top - container.Y, dpi)));
    }

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
