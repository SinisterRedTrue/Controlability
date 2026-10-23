using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace Controlability.App.Services;

/// <summary>Keeps application surfaces in sync with explicit and Windows-driven theme changes.</summary>
internal static class DesignTheme
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        ApplicationThemeManager.Changed += (theme, _) => Apply(theme);
        Apply(ApplicationThemeManager.GetAppTheme());
    }

    private static void Apply(ApplicationTheme theme)
    {
        if (Application.Current is not { } app) return;
        var dark = theme != ApplicationTheme.Light;
        void Brush(string key, string value)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            brush.Freeze();
            app.Resources[key] = brush;
        }
        Brush("AppBackgroundBrush", dark ? "#0D141C" : "#F2F5F9");
        Brush("ApplicationBackgroundBrush", dark ? "#0D141C" : "#F2F5F9");
        Brush("SolidBackgroundFillColorBaseBrush", dark ? "#0D141C" : "#F2F5F9");
        Brush("SolidBackgroundFillColorSecondaryBrush", dark ? "#161F28" : "#FFFFFF");
        Brush("LayerFillColorDefaultBrush", dark ? "#161F28" : "#FFFFFF");
        Brush("CardBackgroundFillColorDefaultBrush", dark ? "#161F28" : "#FFFFFF");
        Brush("SubtleFillColorSecondaryBrush", dark ? "#202C38" : "#E9EFF5");
        Brush("SelectionBrush", dark ? "#173247" : "#D5EAF4");
        Brush("SidebarBrush", dark ? "#101820" : "#E9EFF5");
        Brush("PanelBrush", dark ? "#161F28" : "#FFFFFF");
        Brush("BrandBrush", dark ? "#0BC5EE" : "#007D96");
        Brush("PrimaryTextBrush", dark ? "#061724" : "#FFFFFF");
        Brush("TextOnAccentFillColorPrimaryBrush", dark ? "#061724" : "#FFFFFF");
        Brush("TextFillColorPrimaryBrush", dark ? "#F0F5FC" : "#172536");
        Brush("TextFillColorSecondaryBrush", dark ? "#ACBBCD" : "#475C73");
        Brush("TextFillColorTertiaryBrush", dark ? "#8C9FB7" : "#5E7084");
        Brush("TextFillColorDisabledBrush", dark ? "#93A4BA" : "#637286");
        Brush("ControlFillColorDefaultBrush", dark ? "#202C38" : "#E9EFF5");
        Brush("ControlStrokeColorDefaultBrush", dark ? "#344352" : "#BCCAD8");
        Brush("AccentFillColorDefaultBrush", dark ? "#0BC5EE" : "#007D96");
        Brush("SystemAccentColorPrimaryBrush", dark ? "#0BC5EE" : "#007D96");
        Brush("DeviceEdgeBrush", dark ? "#283541" : "#BCCAD8");
        Brush("DeviceAccentBrush", dark ? "#00ADFA" : "#007D96");
        void Gradient(string key, string top, string bottom)
        {
            var brush = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString(top),
                (Color)ColorConverter.ConvertFromString(bottom), new Point(0, 0), new Point(1, 1));
            brush.Freeze();
            app.Resources[key] = brush;
        }
        Gradient("DeviceBadgeBrush", dark ? "#55D0FA" : "#007D96", dark ? "#22ADEA" : "#007D96");
        Gradient("DeviceShellBrush", dark ? "#202B36" : "#F7FAFD", dark ? "#0B1118" : "#DAE3ED");
        Gradient("DevicePartBrush", dark ? "#303E4D" : "#FFFFFF", dark ? "#111922" : "#C6D4E2");
        var stickBrush = new RadialGradientBrush();
        foreach (var (offset, color) in new[] { (0.5, dark ? "#1D2B3A" : "#D8E6F1"), (0.75, "#25465F"), (0.84, "#142330"), (0.94, "#427393"), (1.0, "#152432") })
            stickBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(color), offset));
        stickBrush.Freeze();
        app.Resources["DeviceStickBrush"] = stickBrush;
        Gradient("DeviceTriggerBrush", dark ? "#25303C" : "#E5EDF5", dark ? "#1C2630" : "#D8E3ED");
        var liveSurface = new RadialGradientBrush(
            (Color)ColorConverter.ConvertFromString(dark ? "#18232E" : "#FFFFFF"),
            (Color)ColorConverter.ConvertFromString(dark ? "#0D151D" : "#EFF4F9"))
        {
            Center = new Point(0.5, 0.45), GradientOrigin = new Point(0.4, 0), RadiusX = 0.8, RadiusY = 1.2,
        };
        liveSurface.Freeze();
        app.Resources["DeviceLiveSurfaceBrush"] = liveSurface;
        Gradient("DeviceSurfaceBrush", dark ? "#151D26" : "#FFFFFF", dark ? "#10171F" : "#F5F8FC");
        Gradient("DeviceButtonBrush", dark ? "#2B3542" : "#FFFFFF", dark ? "#1D2631" : "#E5EDF5");
        Gradient("DeviceSelectionBrush", dark ? "#163951" : "#D5EAF4", dark ? "#1B2C3D" : "#E4F1FA");
    }
}
