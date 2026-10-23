using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Controlability.App.Controls;

/// <summary>Scalable controller and action symbols used by the profile editor.</summary>
public sealed class ProfileGlyph : FrameworkElement
{
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(ProfileGlyph), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? Foreground { get => (Brush?)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(nameof(Symbol), typeof(object), typeof(ProfileGlyph), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public object? Symbol { get => GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }
    public ProfileGlyph() { Width = 28; Height = 28; }
    protected override void OnRender(DrawingContext dc)
    {
        var key = Symbol?.ToString() ?? "";
        var ink = Foreground ?? TryFindResource("TextFillColorPrimaryBrush") as Brush ?? Brushes.White;
        var pen = new Pen(ink, 1.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        dc.PushTransform(new ScaleTransform(ActualWidth / 32, ActualHeight / 32));
        void Path(string path, Brush? fill = null) => dc.DrawGeometry(fill, pen, Geometry.Parse(path));
        void Label(string text, double size = 18, Brush? color = null)
        {
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), size, color ?? ink, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point((32 - ft.Width) / 2, (32 - ft.Height) / 2 - 1));
        }
        if (key is "A" or "B" or "X" or "Y")
        {
            var color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(key switch { "A" => "#52E53B", "B" => "#FF4842", "X" => "#20AFF4", _ => "#FFDB29" }));
            dc.DrawEllipse(key == "A" ? null : color, new Pen(color, 2), new Point(16, 16), 12, 12);
            Label(key, 19, key == "A" ? color : new SolidColorBrush(Color.FromRgb(14, 25, 32)));
        }
        else if (key.Contains("Cross")) Path("M9,9 L23,23 M23,9 L9,23");
        else if (key.Contains("Circle")) dc.DrawEllipse(null, new Pen(Brushes.Tomato, 2), new Point(16, 16), 9, 9);
        else if (key.Contains("Square")) dc.DrawRectangle(null, new Pen(Brushes.Violet, 2), new Rect(7, 7, 18, 18));
        else if (key.Contains("Triangle")) dc.DrawGeometry(null, new Pen(Brushes.Turquoise, 2), Geometry.Parse("M16,6 L27,25 L5,25 Z"));
        else if (key is "PlayStation" or "PS button" or "PS4" or "PS5")
        {
            if (TryFindResource("PlayStationBrandMark") is Geometry mark)
            {
                var b = mark.Bounds; var scale = 29 / b.Width;
                dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, 1 - b.X * scale, (32 - b.Height * scale) / 2 - b.Y * scale));
                dc.DrawGeometry(ink, null, mark); dc.Pop();
            }
        }
        else if (key is "Guide" or "Xbox")
        {
            if (TryFindResource("XboxBrandMark") is Geometry mark)
            {
                var b = mark.Bounds; var scale = 28 / b.Width;
                dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, 2 - b.X * scale, 2 - b.Y * scale)); dc.DrawGeometry(ink, null, mark); dc.Pop();
            }
        }
        else if (key is "Keyboard")
        {
            dc.DrawRoundedRectangle(null, pen, new Rect(2, 6, 28, 20), 2, 2);
            for (var y = 10; y < 22; y += 5) for (var x = 6; x < 28; x += 5) dc.DrawRectangle(ink, null, new Rect(x, y, 2, 2));
            Path("M10,23 L23,23");
        }
        else if (key is "Nintendo" or "SwitchPro") { dc.DrawRoundedRectangle(null, pen, new Rect(3, 2, 11, 28), 4, 4); dc.DrawRoundedRectangle(ink, null, new Rect(18, 2, 11, 28), 4, 4); dc.DrawEllipse(ink, null, new Point(8.5, 10), 2.5, 2.5); dc.DrawEllipse(Brushes.Black, null, new Point(23.5, 21), 2.5, 2.5); }
        else if (key is "Generic" or "XInput") { Path("M9,8 C3,8 2,17 1,25 Q1,31 6,27 L12,21 L20,21 L26,27 Q31,31 31,25 C30,17 29,8 23,8 Z"); Path("M7,12 L7,20 M3,16 L11,16"); dc.DrawEllipse(ink, null, new Point(23, 13), 1.7, 1.7); dc.DrawEllipse(ink, null, new Point(27, 17), 1.7, 1.7); }
        else if (key is "Back" or "Share" || key.Contains("Share")) { dc.DrawRoundedRectangle(null, pen, new Rect(3, 11, 26, 10), 3, 3); dc.DrawEllipse(ink, null, new Point(9, 16), 1.5, 1.5); dc.DrawEllipse(ink, null, new Point(23, 16), 1.5, 1.5); }
        else if (key is "Start" or "Menu") Path("M5,8 L27,8 M5,16 L27,16 M5,24 L27,24");
        else if (key is "Options") { dc.DrawRoundedRectangle(null, pen, new Rect(12, 4, 8, 24), 3, 3); dc.DrawEllipse(ink, null, new Point(16, 10), 1.2, 1.2); dc.DrawEllipse(ink, null, new Point(16, 22), 1.2, 1.2); }
        else if (key.StartsWith("Dpad") || key.StartsWith("D-pad")) Path("M12,3 L20,3 L20,12 L29,12 L29,20 L20,20 L20,29 L12,29 L12,20 L3,20 L3,12 L12,12 Z");
        else if (key is "Up" or "Down" or "Left" or "Right") Label(key switch { "Up" => "↑", "Down" => "↓", "Left" => "←", _ => "→" }, 30);
        else if (key is "LeftTrigger" or "RightTrigger") { Path("M7,3 L25,3 L25,22 Q22,29 3,30 L7,3 Z"); Label(key == "LeftTrigger" ? "LT" : "RT", 13); }
        else if (key.Contains("Thumb") || key.Contains("stick", StringComparison.OrdinalIgnoreCase) || key is "L3" or "R3") { dc.DrawEllipse(null, pen, new Point(16, 15), 12, 12); Label(key is "L3" or "R3" ? key : key.StartsWith("Left") ? "L" : "R", 15); Path("M8,29 Q16,33 24,29"); }
        else if (key.Contains("Shoulder") || key is "LB" or "RB" or "L1" or "R1") { dc.DrawRoundedRectangle(null, pen, new Rect(1, 5, 30, 22), 4, 4); Label(key.Contains("Left") ? "LB" : key.Contains("Right") ? "RB" : key, 14); }
        else if (key is "Bind") { Path("M12,20 L20,12 M14,10 L19,5 Q23,2 26,6 Q29,9 25,13 L21,17 M18,22 L13,27 Q9,30 6,26 Q3,23 7,19 L11,15"); }
        else if (key is "Clear") Path("M9,9 L23,23 M23,9 L9,23");
        else if (key is "Save") { Path("M5,3 L24,3 L29,8 L29,29 L3,29 L3,3 Z M10,3 L10,13 L23,13 L23,3 M9,29 L9,19 L23,19 L23,29"); }
        else if (key is "Revert" or "Reset") { Path("M8,10 A11,11 0 1 1 5,20 M8,3 L8,11 L17,11"); }
        else if (key is "Import" or "Export") { Path(key == "Import" ? "M16,3 L16,22 M9,15 L16,22 L23,15 M4,23 L4,29 L28,29 L28,23" : "M16,23 L16,4 M9,11 L16,4 L23,11 M4,23 L4,29 L28,29 L28,23"); }
        else if (key is "Duplicate") Path("M12,4 L27,4 L27,24 L12,24 Z M8,9 L5,9 L5,29 L21,29 L21,27");
        else if (key is "Delete") Path("M5,8 L27,8 M12,8 L12,3 L21,3 L21,8 M8,8 L10,29 L23,29 L25,8 M14,12 L14,25 M20,12 L20,25");
        else if (key is "Document") Path("M7,2 L20,2 L27,9 L27,30 L7,30 Z M20,2 L20,10 L27,10 M12,16 L22,16 M12,21 L22,21 M12,26 L19,26");
        else Label(key.Length > 3 ? "⊙" : key, 20);
        dc.Pop();
    }
}
