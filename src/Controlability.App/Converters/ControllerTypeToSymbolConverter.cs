using System.Globalization;
using System.Windows.Data;
using Controlability.Core.Models;
using Wpf.Ui.Controls;

namespace Controlability.App.Converters;

/// <summary>Fluent icons have no PS4/PS5/Switch logos; the keyboard and the gamepad get their own glyph.</summary>
public sealed class ControllerTypeToSymbolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ControllerType.Keyboard => SymbolRegular.Keyboard24,
        ControllerType.Xbox => SymbolRegular.XboxController24,
        _ => SymbolRegular.Games24,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
