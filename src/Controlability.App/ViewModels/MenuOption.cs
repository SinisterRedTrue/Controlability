using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Controlability.App.ViewModels;

/// <summary>One entry of a context menu: a header, an optional check mark and the command it runs.</summary>
public sealed partial class MenuOption(string header, Action execute, bool isChecked = false) : ObservableObject
{
    public string Header { get; } = header;

    public bool IsChecked { get; } = isChecked;

    public IRelayCommand Command { get; } = new RelayCommand(execute);
}
