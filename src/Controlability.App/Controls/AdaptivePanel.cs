using System.Windows;
using System.Windows.Controls;

namespace Controlability.App.Controls;

/// <summary>Two columns when space permits, a reading-order stack at narrow widths.</summary>
public sealed class AdaptivePanel : Panel
{
    public double Breakpoint { get; set; } = 680;
    public double FirstColumnRatio { get; set; } = 0.5;
    public double Gap { get; set; } = 16;
    public bool StretchRows { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var wide = double.IsFinite(availableSize.Width) && availableSize.Width >= Breakpoint;
        double height = 0, width = 0, rowHeight = 0;
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var childWidth = wide ? Math.Max(0, availableSize.Width - Gap) * (i % 2 == 0 ? FirstColumnRatio : 1 - FirstColumnRatio) : availableSize.Width;
            InternalChildren[i].Measure(new Size(childWidth, double.PositiveInfinity));
            var desired = InternalChildren[i].DesiredSize;
            width = Math.Max(width, desired.Width);
            rowHeight = Math.Max(rowHeight, desired.Height);
            if (!wide || i % 2 == 1 || i == InternalChildren.Count - 1)
            {
                height += rowHeight + (height > 0 ? Gap : 0);
                rowHeight = 0;
            }
        }
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var wide = finalSize.Width >= Breakpoint;
        var step = wide ? 2 : 1;
        var rows = (InternalChildren.Count + step - 1) / step;
        double natural = 0;
        for (var i = 0; i < InternalChildren.Count; i += step)
        {
            var rowHeight = InternalChildren[i].DesiredSize.Height;
            if (wide && i + 1 < InternalChildren.Count) rowHeight = Math.Max(rowHeight, InternalChildren[i + 1].DesiredSize.Height);
            natural += rowHeight;
        }

        // Spare height goes to the rows, so the last row ends exactly at the bottom of the space it is given.
        var extra = StretchRows && double.IsFinite(finalSize.Height) && rows > 0
            ? Math.Max(0, (finalSize.Height - natural - Gap * (rows - 1)) / rows)
            : 0;
        double y = 0;
        for (var i = 0; i < InternalChildren.Count; i += step)
        {
            var height = InternalChildren[i].DesiredSize.Height;
            if (wide && i + 1 < InternalChildren.Count) height = Math.Max(height, InternalChildren[i + 1].DesiredSize.Height);
            height += extra;
            var width = wide ? Math.Max(0, finalSize.Width - Gap) * FirstColumnRatio : finalSize.Width;
            InternalChildren[i].Arrange(new Rect(0, y, width, height));
            if (wide && i + 1 < InternalChildren.Count)
                InternalChildren[i + 1].Arrange(new Rect(width + Gap, y, Math.Max(0, finalSize.Width - width - Gap), height));
            y += height + Gap;
        }
        return finalSize;
    }
}
