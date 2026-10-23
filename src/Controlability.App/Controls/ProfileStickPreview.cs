using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Controlability.App.ViewModels;

namespace Controlability.App.Controls;

/// <summary>Stick travel, dead zone and live coordinate readout for the profile editor.</summary>
public sealed class ProfileStickPreview : FrameworkElement
{
    public static readonly DependencyProperty LiveProperty = DependencyProperty.Register(nameof(Live), typeof(LivePadViewModel), typeof(ProfileStickPreview));
    public static readonly DependencyProperty IsRightProperty = DependencyProperty.Register(nameof(IsRight), typeof(bool), typeof(ProfileStickPreview));
    public static readonly DependencyProperty InnerProperty = DependencyProperty.Register(nameof(Inner), typeof(double), typeof(ProfileStickPreview), new FrameworkPropertyMetadata(.08, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty OuterProperty = DependencyProperty.Register(nameof(Outer), typeof(double), typeof(ProfileStickPreview), new FrameworkPropertyMetadata(.98, FrameworkPropertyMetadataOptions.AffectsRender));
    public LivePadViewModel? Live { get => (LivePadViewModel?)GetValue(LiveProperty); set => SetValue(LiveProperty, value); }
    public bool IsRight { get => (bool)GetValue(IsRightProperty); set => SetValue(IsRightProperty, value); }
    public double Inner { get => (double)GetValue(InnerProperty); set => SetValue(InnerProperty, value); }
    public double Outer { get => (double)GetValue(OuterProperty); set => SetValue(OuterProperty, value); }
    private PadPose _pose;
    public ProfileStickPreview() { Height = 195; _ = new FrameTicker(this, () => { Live?.Poll(); var next = Live?.Raw ?? default; if (next != _pose) { _pose = next; InvalidateVisual(); } }); }
    protected override void OnRender(DrawingContext dc)
    {
        var muted = (Brush?)TryFindResource("TextFillColorSecondaryBrush") ?? Brushes.Gray;
        var edge = (Brush?)TryFindResource("DeviceEdgeBrush") ?? Brushes.Gray;
        var accent = (Brush?)TryFindResource("BrandBrush") ?? Brushes.Cyan;
        var panel = (Brush?)TryFindResource("AppBackgroundBrush") ?? Brushes.Black;
        var cx = ActualWidth * .33; var cy = 95d; var r = 87d;
        var x = IsRight ? _pose.RightX : _pose.LeftX; var y = IsRight ? _pose.RightY : _pose.LeftY;
        dc.DrawEllipse(panel, new Pen(muted, 1.5), new Point(cx, cy), r, r);
        dc.DrawEllipse(null, new Pen(edge, 1), new Point(cx, cy), r * Outer, r * Outer);
        dc.DrawEllipse((Brush?)TryFindResource("ControlFillColorDefaultBrush"), null, new Point(cx, cy), r * Inner, r * Inner);
        dc.DrawLine(new Pen(edge, 1), new Point(cx - r, cy), new Point(cx + r, cy));
        dc.DrawLine(new Pen(edge, 1), new Point(cx, cy - r), new Point(cx, cy + r));
        dc.DrawEllipse(accent, null, new Point(cx + x * r, cy - y * r), 7, 7);
        var box = new Rect(cx + r + 20, 34, Math.Max(110, ActualWidth - cx - r - 26), 94);
        dc.DrawRoundedRectangle(null, new Pen(edge, 1), box, 5, 5);
        var i = 0;
        foreach (var value in new[] { $"X:   {x:F2}", $"Y:   {y:F2}", $"Magnitude:   {Math.Sqrt(x * x + y * y):F2}" })
        {
            dc.DrawText(new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, muted, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(box.X + 14, box.Y + 10 + i++ * 24));
        }
    }
}
