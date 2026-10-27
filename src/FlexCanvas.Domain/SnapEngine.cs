namespace FlexCanvas.Domain;

/// <summary>A logical-DIP guide spanning the affected rectangles.</summary>
public sealed record SnapGuide(bool Vertical, double Coordinate, double Start, double End);

/// <summary>Snapped geometry and explicit alignment guides, with no native side effects.</summary>
public sealed record SnapResult(ContainerLayout Container, IReadOnlyList<SnapGuide> Guides);

/// <summary>Deterministic edge, adjacency and equal-gap snapping for the finite layout.</summary>
public static class SnapEngine
{
    /// <summary>Snaps strictly inside the threshold. Equal-distance ties use stable target IDs and candidate order.</summary>
    public static SnapResult Move(CanvasLayout layout, ContainerLayout proposed, double threshold = 12)
    {
        if (!LayoutValidator.Validate(layout).Success || !double.IsFinite(threshold) || threshold < 0
            || !LayoutValidator.Validate(new CanvasLayout([proposed])).Success || proposed.IsPinned)
            return new(proposed, []);
        var others = layout.Containers.Where(item => item.Id != proposed.Id).OrderBy(item => item.Id).ToArray();
        var xs = new List<(double Position, SnapGuide Guide)>();
        var ys = new List<(double Position, SnapGuide Guide)>();
        foreach (var target in others)
        {
            double top = Math.Min(proposed.Y, target.Y), bottom = Math.Max(proposed.Y + proposed.Height, target.Y + target.Height);
            double left = Math.Min(proposed.X, target.X), right = Math.Max(proposed.X + proposed.Width, target.X + target.Width);
            foreach (double edge in new[] { target.X, target.X + target.Width })
            {
                xs.Add((edge, new(true, edge, top, bottom)));
                xs.Add((edge - proposed.Width, new(true, edge, top, bottom)));
            }
            foreach (double edge in new[] { target.Y, target.Y + target.Height })
            {
                ys.Add((edge, new(false, edge, left, right)));
                ys.Add((edge - proposed.Height, new(false, edge, left, right)));
            }
        }
        // Equal gaps extend an existing pair; do not infer a gap from overlapping rectangles.
        foreach (var first in others)
        foreach (var second in others.Where(item => item.Id != first.Id))
        {
            double gapX = second.X - first.X - first.Width;
            if (gapX >= 0 && Math.Min(first.Y + first.Height, second.Y + second.Height) > Math.Max(first.Y, second.Y))
            {
                double gapPositionX = second.X + second.Width + gapX;
                xs.Add((gapPositionX, new(true, gapPositionX, Math.Min(first.Y, proposed.Y), Math.Max(second.Y + second.Height, proposed.Y + proposed.Height))));
            }
            double gapY = second.Y - first.Y - first.Height;
            if (gapY >= 0 && Math.Min(first.X + first.Width, second.X + second.Width) > Math.Max(first.X, second.X))
            {
                double gapPositionY = second.Y + second.Height + gapY;
                ys.Add((gapPositionY, new(false, gapPositionY, Math.Min(first.X, proposed.X), Math.Max(second.X + second.Width, proposed.X + proposed.Width))));
            }
        }
        var guides = new List<SnapGuide>();
        var xCandidate = xs.Where(item => item.Position >= 0 && item.Position <= CanvasLayout.Extent - proposed.Width
                && Math.Abs(item.Position - proposed.X) < threshold)
            .OrderBy(item => Math.Abs(item.Position - proposed.X)).FirstOrDefault();
        var yCandidate = ys.Where(item => item.Position >= 0 && item.Position <= CanvasLayout.Extent - proposed.Height
                && Math.Abs(item.Position - proposed.Y) < threshold)
            .OrderBy(item => Math.Abs(item.Position - proposed.Y)).FirstOrDefault();
        double x = proposed.X, y = proposed.Y;
        if (xCandidate.Guide is not null) { x = xCandidate.Position; guides.Add(xCandidate.Guide); }
        if (yCandidate.Guide is not null) { y = yCandidate.Position; guides.Add(yCandidate.Guide); }
        return new(proposed with { X = x, Y = y }, guides.AsReadOnly());
    }
}
