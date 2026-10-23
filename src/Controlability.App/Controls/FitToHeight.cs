using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Controlability.App.Controls;

/// <summary>
/// Shrinks its child, never below <see cref="MinScale"/>, until it fits the available height, so a page needs no
/// scrolling at ordinary window sizes. The child is laid out at the scaled-up width so it still fills the page.
/// </summary>
public sealed class FitToHeight : Decorator
{
    private double _scale = 1;

    public double MinScale { get; set; } = 0.6;

    protected override Size MeasureOverride(Size available)
    {
        if (Child is not { } child)
        {
            return default;
        }

        if (double.IsInfinity(available.Width) || double.IsInfinity(available.Height))
        {
            child.RenderTransform = Transform.Identity;
            _scale = 1;
            child.Measure(available);
            return child.DesiredSize;
        }

        var scale = 1.0;
        for (var pass = 0; pass < 4; pass++)
        {
            child.Measure(new Size(available.Width / scale, double.PositiveInfinity));
            var wanted = Math.Clamp(available.Height / Math.Max(1, child.DesiredSize.Height), MinScale, 1);
            if (Math.Abs(wanted - scale) < 0.002)
            {
                scale = wanted;
                break;
            }

            scale = wanted;
        }

        _scale = scale;
        child.RenderTransformOrigin = new Point(0, 0);
        child.RenderTransform = scale < 1 ? new ScaleTransform(scale, scale) : Transform.Identity;
        child.Measure(new Size(available.Width / scale, available.Height / scale));
        return available;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width / _scale, finalSize.Height / _scale));
        return finalSize;
    }
}
