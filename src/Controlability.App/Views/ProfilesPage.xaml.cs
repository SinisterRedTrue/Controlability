using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Controlability.App.ViewModels;
using Controlability.Core.Keyboard;

namespace Controlability.App.Views;

public partial class ProfilesPage : Page
{
    private readonly ProfilesViewModel _viewModel;

    public ProfilesPage(ProfilesViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => viewModel.Reload();
        Unloaded += (_, _) => viewModel.Editor?.CancelBind();
    }

    /// <summary>Esc stops waiting for a controller button; while a key is being captured the next key becomes the binding.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel.Editor is not { } editor)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (editor.IsBinding)
        {
            if (key == Key.Escape)
            {
                editor.CancelBind();
                e.Handled = true;
            }

            return;
        }

        if (!editor.IsCapturing)
        {
            return;
        }

        var name = KeyCodes.NameOf(KeyInterop.VirtualKeyFromKey(key));
        e.Handled = true;
        if (!name.StartsWith("VK_", StringComparison.Ordinal))
        {
            editor.ApplyKeyCapture(name);
        }
    }

    /// <summary>While capturing a key, a click anywhere but on a button becomes a mouse binding.</summary>
    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.Editor is not { IsCapturing: true } editor || IsOnButton(e.OriginalSource as DependencyObject))
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
            editor.ApplyKeyCapture(MouseButtonNames.NameOf(button));
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
