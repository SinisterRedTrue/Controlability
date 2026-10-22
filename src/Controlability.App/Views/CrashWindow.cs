using System.Windows;
using System.Windows.Controls;
using Controlability.App.Resources;

namespace Controlability.App.Views;

/// <summary>
/// The dialog after a crash: what happened in plain words, "Copy diagnostics" and "Close". Built in code so it needs
/// nothing from the (possibly broken) theme or view-model layer.
/// </summary>
public sealed class CrashWindow : Window
{
    public CrashWindow(Exception exception, Func<bool> copyDiagnostics)
    {
        Title = Strings.CrashTitle;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;

        var status = new TextBlock { Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        var copy = new Button { Content = Strings.CrashCopy, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 4, 12, 4) };
        copy.Click += (_, _) => status.Text = copyDiagnostics() ? Strings.DiagnosticsCopied : Strings.DiagnosticsCopyFailed;
        var close = new Button { Content = Strings.CrashClose, IsDefault = true, Padding = new Thickness(12, 4, 12, 4) };
        close.Click += (_, _) => Close();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(copy);
        buttons.Children.Add(close);

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = Strings.CrashHeading, FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = Strings.CrashText, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = exception.Message, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        panel.Children.Add(status);
        panel.Children.Add(buttons);
        Content = panel;
    }
}
