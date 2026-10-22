using System.Windows;
using System.Windows.Media;

namespace Controlability.App.Controls;

/// <summary>
/// Draws a response curve: input on the horizontal axis, output on the vertical one, both 0..1, with the straight line
/// of a linear curve for comparison. <see cref="Samples"/> holds the output at evenly spaced inputs.
/// </summary>
public sealed class CurveGraph : FrameworkElement
{
    public static readonly DependencyProperty SamplesProperty =
        DependencyProperty.Register(
            nameof(Samples),
            typeof(IReadOnlyList<float>),
            typeof(CurveGraph),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public CurveGraph()
    {
        Width = 120;
        Height = 120;
        SnapsToDevicePixels = true;
    }

    public IReadOnlyList<float>? Samples
    {
        get => (IReadOnlyList<float>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var frame = TryFindResource("ControlFillColorDefaultBrush") as Brush ?? Brushes.Transparent;
        var edge = TryFindResource("TextFillColorTertiaryBrush") as Brush ?? Brushes.Gray;
        var accent = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DodgerBlue;

        var area = new Rect(0.5, 0.5, Math.Max(0, ActualWidth - 1), Math.Max(0, ActualHeight - 1));
        drawingContext.DrawRectangle(frame, new Pen(edge, 1), area);

        var guide = new Pen(edge, 1) { DashStyle = DashStyles.Dot };
        drawingContext.DrawLine(guide, new Point(area.Left, area.Bottom), new Point(area.Right, area.Top));

        if (Samples is not { Count: > 1 } samples)
        {
            return;
        }

        var points = new StreamGeometry();
        using (var context = points.Open())
        {
            for (var i = 0; i < samples.Count; i++)
            {
                var x = area.Left + (area.Width * i / (samples.Count - 1));
                var y = area.Bottom - (area.Height * Math.Clamp(samples[i], 0f, 1f));
                if (i == 0)
                {
                    context.BeginFigure(new Point(x, y), false, false);
                }
                else
                {
                    context.LineTo(new Point(x, y), true, true);
                }
            }
        }

        points.Freeze();
        drawingContext.DrawGeometry(null, new Pen(accent, 2) { LineJoin = PenLineJoin.Round }, points);
    }
}
