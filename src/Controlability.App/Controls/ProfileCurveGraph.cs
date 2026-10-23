using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Controlability.App.Controls;

/// <summary>Labelled response curve with input/output axes and a tenth-step grid.</summary>
public sealed class ProfileCurveGraph : FrameworkElement
{
    public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(nameof(Samples), typeof(IReadOnlyList<float>), typeof(ProfileCurveGraph), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<float>? Samples { get => (IReadOnlyList<float>?)GetValue(SamplesProperty); set => SetValue(SamplesProperty, value); }
    public ProfileCurveGraph() { Height = 175; }
    protected override void OnRender(DrawingContext dc)
    {
        var ink = (Brush?)TryFindResource("TextFillColorSecondaryBrush") ?? Brushes.Gray;
        var edge = (Brush?)TryFindResource("DeviceEdgeBrush") ?? Brushes.Gray;
        var accent = (Brush?)TryFindResource("BrandBrush") ?? Brushes.Cyan;
        var rect = new Rect(48, 8, Math.Max(1, ActualWidth - 63), Math.Max(1, ActualHeight - 52));
        void Label(string value, double x, double y, bool center = false)
        {
            var ft = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, ink, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(center ? x - ft.Width / 2 : x, y));
        }
        for (var i = 0; i <= 10; i++)
        {
            var x = rect.Left + rect.Width * i / 10;
            var y = rect.Bottom - rect.Height * i / 10;
            dc.DrawLine(new Pen(edge, i % 2 == 0 ? 1 : .4), new Point(x, rect.Top), new Point(x, rect.Bottom));
            dc.DrawLine(new Pen(edge, i % 2 == 0 ? 1 : .4), new Point(rect.Left, y), new Point(rect.Right, y));
            if (i % 2 == 0) { Label((i / 10d).ToString("F1", CultureInfo.InvariantCulture), x, rect.Bottom + 10, true); Label((i / 10d).ToString("F1", CultureInfo.InvariantCulture), 22, y - 8); }
        }
        dc.DrawLine(new Pen(ink, 1), rect.BottomLeft, rect.BottomRight); dc.DrawLine(new Pen(ink, 1), rect.BottomLeft, rect.TopLeft);
        Label("Input", rect.Left + rect.Width / 2, rect.Bottom + 29, true);
        dc.PushTransform(new RotateTransform(-90, 9, rect.Top + rect.Height / 2)); Label("Output", 9, rect.Top + rect.Height / 2 - 8, true); dc.Pop();
        var points = Samples ?? [0f, 1f];
        if (points.Count < 2) return;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
            for (var i = 0; i < points.Count; i++)
            {
                var p = new Point(rect.Left + rect.Width * i / (points.Count - 1), rect.Bottom - rect.Height * Math.Clamp(points[i], 0, 1));
                if (i == 0) context.BeginFigure(p, false, false); else context.LineTo(p, true, false);
            }
        dc.DrawGeometry(null, new Pen(accent, 2), geometry);
    }
}
