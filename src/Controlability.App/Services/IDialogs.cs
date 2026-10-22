using System.Windows;
using Microsoft.Win32;

namespace Controlability.App.Services;

/// <summary>File pickers and yes/no questions, so view models need no WPF types and can be tested.</summary>
public interface IDialogs
{
    /// <summary>Returns the chosen path, or null if the user cancelled. <paramref name="filter"/> is a Win32 filter string.</summary>
    string? PickOpenFile(string title, string filter);

    string? PickSaveFile(string title, string filter, string fileName);

    bool Confirm(string title, string message);
}

public sealed class WpfDialogs : IDialogs
{
    public string? PickOpenFile(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSaveFile(string title, string filter, string fileName)
    {
        var dialog = new SaveFileDialog { Title = title, Filter = filter, FileName = fileName, OverwritePrompt = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool Confirm(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
