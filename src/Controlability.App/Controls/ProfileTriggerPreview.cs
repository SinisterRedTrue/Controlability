using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Controlability.App.ViewModels;

namespace Controlability.App.Controls;

/// <summary>Live physical/output trigger columns and the transfer curve for the current draft.</summary>
public sealed class ProfileTriggerPreview : FrameworkElement
{
    public static readonly DependencyProperty LiveProperty = DependencyProperty.Register(nameof(Live), typeof(LivePadViewModel), typeof(ProfileTriggerPreview));
    public static readonly DependencyProperty DigitalProperty = DependencyProperty.Register(nameof(Digital), typeof(bool), typeof(ProfileTriggerPreview), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ThresholdProperty = DependencyProperty.Register(nameof(Threshold), typeof(double), typeof(ProfileTriggerPreview), new FrameworkPropertyMetadata(.5, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty DeadZoneProperty = DependencyProperty.Register(nameof(DeadZone), typeof(double), typeof(ProfileTriggerPreview), new FrameworkPropertyMetadata(.05, FrameworkPropertyMetadataOptions.AffectsRender));
    public LivePadViewModel? Live { get => (LivePadViewModel?)GetValue(LiveProperty); set => SetValue(LiveProperty, value); }
    public bool Digital { get => (bool)GetValue(DigitalProperty); set => SetValue(DigitalProperty, value); }
    public double Threshold { get => (double)GetValue(ThresholdProperty); set => SetValue(ThresholdProperty, value); }
    public double DeadZone { get => (double)GetValue(DeadZoneProperty); set => SetValue(DeadZoneProperty, value); }
    private PadPose _raw, _output;
    public ProfileTriggerPreview() { _ = new FrameTicker(this, () => { Live?.Poll(); var raw = Live?.Raw ?? default; var output = Live?.Output ?? default; if (raw != _raw || output != _output) { _raw = raw; _output = output; InvalidateVisual(); } }); }
    protected override Size MeasureOverride(Size available) => new(double.IsInfinity(available.Width) ? 450 : available.Width, Height);
    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var ink = TryFindResource("TextFillColorPrimaryBrush") as Brush ?? Brushes.White;
        var muted = TryFindResource("TextFillColorSecondaryBrush") as Brush ?? Brushes.Gray;
        var edge = TryFindResource("DeviceEdgeBrush") as Brush ?? Brushes.Gray;
        var accent = TryFindResource("BrandBrush") as Brush ?? Brushes.Cyan;
        var panel = TryFindResource("AppBackgroundBrush") as Brush ?? Brushes.Black;
        void Label(string value, double x, double y, double size = 13, bool center = false, Brush? brush = null)
        {
            var ft = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush ?? ink, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(center ? x - ft.Width / 2 : x, y));
        }
        var half = (w - 10) / 2;
        for (var side = 0; side < 2; side++)
        {
            var x = side * (half + 10);
            dc.DrawRoundedRectangle(null, new Pen(edge, 1), new Rect(x, 0, half, 306), 7, 7);
            Label(side == 0 ? "LT (L2)" : "RT (R2)", x + half / 2, 10, 16, true);
            Label("Physical", x + half * .34, 45, 12, true, muted); Label("input", x + half * .34, 64, 12, true, muted);
            Label("Xbox", x + half * .75, 45, 12, true, muted); Label("output", x + half * .75, 64, 12, true, muted);
            Label("100%", x + 8, 87, 11, brush: muted); Label("50%", x + 15, 170, 11, brush: muted); Label("0%", x + 20, 250, 11, brush: muted);
            var values = side == 0 ? new[] { _raw.LeftTrigger, _output.LeftTrigger } : new[] { _raw.RightTrigger, _output.RightTrigger };
            for (var i = 0; i < 2; i++)
            {
                var cx = x + half * (i == 0 ? .34 : .75);
                dc.DrawRoundedRectangle(panel, new Pen(muted, 1), new Rect(cx - 19, 92, 38, 166), 4, 4);
                var h = values[i] * 164;
                if (h > 0) dc.DrawRoundedRectangle(accent, null, new Rect(cx - 18, 257 - h, 36, h), 3, 3);
                Label(values[i].ToString("P0", CultureInfo.InvariantCulture), cx, 270, 17, true);
            }
            dc.DrawLine(new Pen(muted, 1) { DashStyle = DashStyles.Dash }, new Point(x + 45, 175), new Point(x + half - 14, 175));
        }
        dc.DrawRoundedRectangle(null, new Pen(edge, 1), new Rect(0, 320, w, 265), 7, 7);
        Label(Digital ? "Input → Output transfer (digital mode)" : "Input → Output transfer (analog mode)", 10, 330, 15);
        var area = new Rect(65, 370, Math.Max(10, w - 86), 160);
        for (var i = 0; i <= 10; i++)
        {
            var pen = new Pen(edge, i % 5 == 0 ? 1 : .4);
            dc.DrawLine(pen, new Point(area.Left + area.Width * i / 10, area.Top), new Point(area.Left + area.Width * i / 10, area.Bottom));
            dc.DrawLine(pen, new Point(area.Left, area.Top + area.Height * i / 10), new Point(area.Right, area.Top + area.Height * i / 10));
        }
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(area.BottomLeft, false, false);
            if (Digital)
            {
                var thresholdX = area.Left + area.Width * Math.Clamp(Threshold, 0, 1);
                ctx.LineTo(new Point(thresholdX, area.Bottom), true, false); ctx.LineTo(new Point(thresholdX, area.Top), true, false);
            }
            else ctx.LineTo(new Point(area.Left + area.Width * Math.Clamp(DeadZone, 0, 1), area.Bottom), true, false);
            ctx.LineTo(area.TopRight, true, false);
        }
        dc.DrawGeometry(null, new Pen(accent, 3), geometry);
        Label("100%", 20, area.Top - 8, 12, brush: muted); Label("50%", 25, area.Top + area.Height / 2 - 8, 12, brush: muted); Label("0%", 34, area.Bottom - 8, 12, brush: muted);
        Label("0.00", area.Left, 540, 12, true, muted); Label("0.50", area.Left + area.Width / 2, 540, 12, true, muted); Label("1.00", area.Right, 540, 12, true, muted); Label("Physical input", area.Left + area.Width / 2, 562, 12, true, muted);
    }
}
