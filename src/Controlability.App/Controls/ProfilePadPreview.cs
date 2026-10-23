using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Controlability.App.ViewModels;
using Controlability.Core.Models;

namespace Controlability.App.Controls;

/// <summary>A shaded Xbox controller with live output and mapping selection feedback.</summary>
public sealed class ProfilePadPreview : FrameworkElement
{
    public static readonly DependencyProperty LiveProperty = DependencyProperty.Register(nameof(Live), typeof(LivePadViewModel), typeof(ProfilePadPreview), new PropertyMetadata(null));
    public static readonly DependencyProperty HighlightedControlProperty = DependencyProperty.Register(nameof(HighlightedControl), typeof(string), typeof(ProfilePadPreview), new FrameworkPropertyMetadata("A", FrameworkPropertyMetadataOptions.AffectsRender));
    public LivePadViewModel? Live { get => (LivePadViewModel?)GetValue(LiveProperty); set => SetValue(LiveProperty, value); }
    public string HighlightedControl { get => (string)GetValue(HighlightedControlProperty); set => SetValue(HighlightedControlProperty, value); }
    private PadPose? _pose;
    public ProfilePadPreview() { _ = new FrameTicker(this, () => { Live?.Poll(); var next = Live?.Output; if (next != _pose) { _pose = next; InvalidateVisual(); } }); }
    private static Brush Solid(string value) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
    private static Brush Gradient(string start, string end) => new LinearGradientBrush(((SolidColorBrush)Solid(start)).Color, ((SolidColorBrush)Solid(end)).Color, new Point(0, 0), new Point(.7, 1));
    protected override Size MeasureOverride(Size available) => new(double.IsInfinity(available.Width) ? 350 : available.Width, 300);
    protected override void OnRender(DrawingContext dc)
    {
        var scale = Math.Min(ActualWidth / 370, ActualHeight / 310);
        dc.PushTransform(new TranslateTransform((ActualWidth - 370 * scale) / 2, (ActualHeight - 310 * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        void Path(string data, Brush fill, string edge = "#060B10", double width = 1.3) => dc.DrawGeometry(fill, new Pen(Solid(edge), width), Geometry.Parse(data));
        void Ellipse(double x, double y, double r, Brush fill, string edge = "#070B10", double width = 2) => dc.DrawEllipse(fill, new Pen(Solid(edge), width), new Point(x, y), r, r);
        bool Active(string key) => HighlightedControl == key || (Enum.TryParse<XboxButton>(key, out var button) && ((_pose?.Buttons ?? XboxButton.None) & button) != 0);
        void Label(string text, double x, double y, double size, Brush brush)
        {
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(x - ft.Width / 2, y - ft.Height / 2));
        }
        Path("M47,40 Q51,14 98,18 L117,34 Z", Gradient("#4C5661", "#20262D"), "#6A7480");
        Path("M253,34 L272,18 Q319,14 323,40 Z", Gradient("#4C5661", "#20262D"), "#6A7480");
        if (Active("LeftShoulder")) Path("M47,40 Q51,14 98,18 L117,34 Z", Solid("#0BC5EE"), "#53DEFF");
        if (Active("RightShoulder")) Path("M253,34 L272,18 Q319,14 323,40 Z", Solid("#0BC5EE"), "#53DEFF");
        var body = "M64,31 C38,37 28,77 19,119 C10,163 1,218 7,254 C11,288 30,296 49,278 L104,218 Q113,208 137,208 L233,208 Q257,208 266,218 L321,278 C340,296 359,288 363,254 C369,218 360,163 351,119 C342,77 332,37 306,31 Q255,17 223,32 L147,32 Q115,17 64,31 Z";
        Path(body, Gradient("#414A54", "#12191F"), "#05090D", 2.5);
        Path("M64,34 Q100,23 145,37 L225,37 Q270,23 306,34", Brushes.Transparent, "#6B7580", 1);
        Path("M11,246 C17,209 28,182 66,198 Q88,209 104,218 L49,278 Q22,302 11,266 Z", Gradient("#252E37", "#121820"));
        Path("M359,246 C353,209 342,182 304,198 Q282,209 266,218 L321,278 Q348,302 359,266 Z", Gradient("#252E37", "#121820"));
        void Stick(double x, double y, bool left)
        {
            Ellipse(x, y, 31, Gradient("#10161C", "#3E4954"), "#0C1117");
            var selected = Active(left ? "LeftThumb" : "RightThumb");
            Ellipse(x, y, 25, Gradient("#505C68", "#131B23"), selected ? "#0BC5EE" : "#56616B", 1.5);
            var dx = left ? _pose?.LeftX ?? 0 : _pose?.RightX ?? 0;
            var dy = left ? _pose?.LeftY ?? 0 : _pose?.RightY ?? 0;
            Ellipse(x + dx * 7, y - dy * 7, 19, Gradient("#17222C", "#0D141B"), "#0A1017", 1);
        }
        Stick(87, 93, true); Stick(238, 158, false);
        Ellipse(185, 56, 15, Gradient("#F6F8FA", "#AEB8C3"));
        if (TryFindResource("XboxBrandMark") is Geometry mark)
        {
            var b = mark.Bounds; var s = 28 / b.Width;
            dc.PushTransform(new MatrixTransform(s, 0, 0, s, 171 - b.X * s, 42 - b.Y * s)); dc.DrawGeometry(Brushes.White, null, mark); dc.Pop();
        }
        Ellipse(185, 56, 15, Brushes.Transparent, "#1D2731", 1);
        if (Active("Guide")) Ellipse(185, 56, 17, Brushes.Transparent, "#0BC5EE", 2);
        Ellipse(128, 161, 32, Gradient("#070D13", "#25313C"));
        Path("M118,133 L138,133 L138,151 L156,151 L156,171 L138,171 L138,189 L118,189 L118,171 L100,171 L100,151 L118,151 Z", Gradient("#48535E", "#1B252F"), "#080E15", 2);
        dc.DrawLine(new Pen(Solid("#111922"), 1), new Point(128, 134), new Point(128, 189));
        dc.DrawLine(new Pen(Solid("#111922"), 1), new Point(101, 161), new Point(155, 161));
        foreach (var (key, data) in new[] {
            ("DpadUp", "M118,133 L138,133 L138,151 L128,160 L118,151 Z"),
            ("DpadDown", "M118,189 L138,189 L138,171 L128,162 L118,171 Z"),
            ("DpadLeft", "M100,151 L118,151 L127,161 L118,171 L100,171 Z"),
            ("DpadRight", "M156,151 L138,151 L129,161 L138,171 L156,171 Z") })
            if (Active(key)) Path(data, Solid("#0BC5EE"), "#53DEFF");
        Ellipse(155, 96, 9, Gradient("#454F59", "#101820")); Label("▣", 155, 95, 11, Solid("#9DA7B2"));
        Ellipse(216, 96, 9, Gradient("#454F59", "#101820")); Label("≡", 216, 94, 14, Solid("#9DA7B2"));
        if (Active("Back")) Ellipse(155, 96, 10, Brushes.Transparent, "#0BC5EE", 2);
        if (Active("Start")) Ellipse(216, 96, 10, Brushes.Transparent, "#0BC5EE", 2);
        dc.DrawRoundedRectangle(Solid("#17212A"), new Pen(Solid("#080D13"), 2), new Rect(175, 112, 20, 9), 4, 4);
        foreach (var (key, x, y, color) in new[] { ("Y", 293d, 72d, "#FFE82F"), ("X", 263d, 101d, "#14BBF2"), ("B", 322d, 101d, "#FF4C28"), ("A", 292d, 132d, "#65F42E") })
        {
            var button = Enum.Parse<XboxButton>(key);
            var selected = HighlightedControl == key || ((_pose?.Buttons ?? XboxButton.None) & button) != 0;
            if (selected)
            {
                for (var i = 8; i >= 1; i--) dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(28 - i * 2), 0, 210, 255)), i), new Point(x, y), 18, 18);
                Ellipse(x, y, 19, Solid("#122D26"), "#00CEF0", 2.5);
            }
            Ellipse(x, y, 14, Gradient("#27313A", "#03080C"), selected ? color : "#050A10", 1.5);
            Label(key, x, y - 1, 23, Solid(color));
        }
        dc.Pop(); dc.Pop();
    }
}
