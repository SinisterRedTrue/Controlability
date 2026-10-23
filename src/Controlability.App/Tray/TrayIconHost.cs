using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Controlability.App.Resources;
using Controlability.App.Services;
using Controlability.App.ViewModels;
using Controlability.Core.Settings;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace Controlability.App.Tray;

/// <summary>The notification-area icon: state icon, tooltip, menu and balloon notifications. UI thread only.</summary>
public sealed class TrayIconHost : INotifier, IDisposable
{
    private readonly TrayViewModel _viewModel;
    private readonly TaskbarIcon _icon;
    private readonly MenuItem[] _slotItems;
    private readonly MenuItem _pauseItem;
    private readonly MenuItem _modeA;
    private readonly MenuItem _modeB;
    private readonly Dictionary<TrayState, byte[]> _iconData = [];
    private TrayState? _shownState;
    private bool _disposed;

    public TrayIconHost(TrayViewModel viewModel)
    {
        _viewModel = viewModel;

        _slotItems = [.. Enumerable.Range(0, 4).Select(_ => new MenuItem { IsEnabled = false })];
        _pauseItem = new MenuItem { Header = Strings.TrayPause, Command = viewModel.TogglePauseCommand };
        _modeA = new MenuItem { Header = Strings.TrayModeA, Command = viewModel.SetModeCommand, CommandParameter = KeyboardMode.KeyboardFirst };
        _modeB = new MenuItem { Header = Strings.TrayModeB, Command = viewModel.SetModeCommand, CommandParameter = KeyboardMode.KeyboardAsController };

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = Strings.TrayOpen, FontWeight = FontWeights.SemiBold, Command = viewModel.OpenCommand });
        menu.Items.Add(new Separator());
        foreach (var item in _slotItems)
        {
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(_pauseItem);
        var modeMenu = new MenuItem { Header = Strings.TrayMode };
        modeMenu.Items.Add(_modeA);
        modeMenu.Items.Add(_modeB);
        menu.Items.Add(modeMenu);
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = Strings.TrayExit, Command = viewModel.ExitCommand });

        _icon = new TaskbarIcon
        {
            ContextMenu = menu,
            MenuActivation = PopupActivationMode.RightClick,
            LeftClickCommand = viewModel.OpenCommand,
            NoLeftClickDelay = true,
        };
        _icon.ForceCreate(enablesEfficiencyMode: false);

        viewModel.Updated += Redraw;
        Redraw();
    }

    public void Notify(string title, string message) => _icon.ShowNotification(title, message, NotificationIcon.Info);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.Updated -= Redraw;
        _icon.Dispose();
    }

    private void Redraw()
    {
        if (_shownState != _viewModel.State)
        {
            // TaskbarIcon disposes the icon it replaces, so every assignment gets a fresh Icon.
            _icon.Icon = IconFor(_viewModel.State);
            _shownState = _viewModel.State;
        }

        _icon.ToolTipText = _viewModel.Tooltip;
        for (var i = 0; i < _slotItems.Length && i < _viewModel.SlotLines.Count; i++)
        {
            _slotItems[i].Header = _viewModel.SlotLines[i];
        }

        _pauseItem.IsChecked = _viewModel.IsPaused;
        _modeA.IsChecked = _viewModel.Mode == KeyboardMode.KeyboardFirst;
        _modeB.IsChecked = _viewModel.Mode == KeyboardMode.KeyboardAsController;
    }

    private System.Drawing.Icon IconFor(TrayState state)
    {
        if (!_iconData.TryGetValue(state, out var data))
        {
            var name = state switch
            {
                TrayState.Paused => "tray-paused",
                TrayState.Error => "tray-error",
                _ => "tray-normal",
            };
            using var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/Resources/Icons/{name}.ico", UriKind.Absolute))!.Stream;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            data = buffer.ToArray();
            _iconData[state] = data;
        }

        using var icon = new MemoryStream(data);
        return new System.Drawing.Icon(icon);
    }
}
