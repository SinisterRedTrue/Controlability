using System.Windows;
using System.Windows.Media;

namespace Controlability.App.Controls;

/// <summary>
/// Draws a response curve: input on the horizontal axis, output on the vertical one, both 0..1, with the straight line
/// of a linear curve for comparison. <see cref="Samples"/> holds the output at evenly spaced inputs.
/// </summary>
public sealed class CurveGraph : FrameworkElement
{
    private static readonly DependencyProperty FrameBrushProperty = DependencyProperty.Register(
        "FrameBrush", typeof(Brush), typeof(CurveGraph), new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty EdgeBrushProperty = DependencyProperty.Register(
        "EdgeBrush", typeof(Brush), typeof(CurveGraph), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        "AccentBrush", typeof(Brush), typeof(CurveGraph), new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SamplesProperty =
        DependencyProperty.Register(
            nameof(Samples),
            typeof(IReadOnlyList<float>),
            typeof(CurveGraph),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public CurveGraph()
    {
        MinWidth = 120;
        Height = 160;
        SnapsToDevicePixels = true;
        SetResourceReference(FrameBrushProperty, "ControlFillColorDefaultBrush");
        SetResourceReference(EdgeBrushProperty, "TextFillColorTertiaryBrush");
        SetResourceReference(AccentBrushProperty, "AccentFillColorDefaultBrush");
    }

    public IReadOnlyList<float>? Samples
    {
        get => (IReadOnlyList<float>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var frame = (Brush)GetValue(FrameBrushProperty);
        var edge = (Brush)GetValue(EdgeBrushProperty);
        var accent = (Brush)GetValue(AccentBrushProperty);

        var area = new Rect(24, 8, Math.Max(0, ActualWidth - 32), Math.Max(0, ActualHeight - 32));
        if (area.Width <= 0 || area.Height <= 0) return;
        drawingContext.DrawRectangle(frame, new Pen(edge, 1), area);

        for (var i = 1; i < 4; i++)
        {
            var gridPen = new Pen(edge, 0.35);
            drawingContext.DrawLine(gridPen, new Point(area.Left + area.Width * i / 4, area.Top), new Point(area.Left + area.Width * i / 4, area.Bottom));
            drawingContext.DrawLine(gridPen, new Point(area.Left, area.Top + area.Height * i / 4), new Point(area.Right, area.Top + area.Height * i / 4));
        }
        void Label(string text, double x, double y) => drawingContext.DrawText(new FormattedText(text,
            System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11,
            edge, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));
        Label("1", 6, area.Top); Label("0", 6, area.Bottom - 12);
        Label("0", area.Left, area.Bottom + 3); Label("1", area.Right - 7, area.Bottom + 3);
        Label("Input → output", area.Left + Math.Max(0, (area.Width - 90) / 2), area.Bottom + 3);

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
