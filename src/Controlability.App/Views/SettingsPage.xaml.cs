using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Controlability.App.ViewModels;
using Controlability.Core.Keyboard;

namespace Controlability.App.Views;

public partial class SettingsPage : Page
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel, GeneralSettingsViewModel general)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        GeneralPanel.DataContext = general;
        Loaded += (_, _) =>
        {
            viewModel.Refresh();
            general.Refresh();
        };
    }

    /// <summary>While a key is being captured, the next key press becomes the binding.</summary>
    private void OnSectionClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name } && FindName(name) is FrameworkElement section)
        {
            if (SettingsScroll.Content is System.Windows.Media.Visual content)
            {
                var position = section.TransformToAncestor(content).Transform(new Point());
                SettingsScroll.ScrollToVerticalOffset(position.Y);
            }
            FirstFocusable(section)?.Focus();
        }
    }

    private static Control? FirstFocusable(DependencyObject parent)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is Control { Focusable: true, IsEnabled: true, IsVisible: true } control) return control;
            if (FirstFocusable(child) is { } nested) return nested;
        }
        return null;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_viewModel.IsCapturing)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var name = KeyCodes.NameOf(KeyInterop.VirtualKeyFromKey(key));
        e.Handled = true;
        if (!name.StartsWith("VK_", StringComparison.Ordinal))
        {
            _viewModel.ApplyCapture(name);
        }
    }

    /// <summary>While capturing, a click anywhere but on a button becomes a mouse binding.</summary>
    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsCapturing || IsOnButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var button = e.ChangedButton switch
        {
            MouseButton.Left => MouseButtons.Left,
            MouseButton.Right => MouseButtons.Right,
            MouseButton.Middle => MouseButtons.Middle,
            MouseButton.XButton1 => MouseButtons.X1,
            MouseButton.XButton2 => MouseButtons.X2,
            _ => MouseButtons.None,
        };
        if (button != MouseButtons.None)
        {
            _viewModel.ApplyCapture(MouseButtonNames.NameOf(button));
            e.Handled = true;
        }
    }

    private static bool IsOnButton(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is ButtonBase)
            {
                return true;
            }

            element = element is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        return false;
    }
}
