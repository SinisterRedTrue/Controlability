using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Controlability.App.Controls;
using Controlability.App.ViewModels;
using Controlability.Core.Models;

namespace Controlability.App.Views;

public partial class DashboardPage : Page
{
    private readonly DashboardViewModel _viewModel;
    private Point _dragStart;

    private void OnDashboardLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= 1000;
        DetectedColumn.Width = new GridLength(wide ? 360 : 0);
        Grid.SetColumn(DetectedPanel, wide ? 1 : 0);
        Grid.SetRow(DetectedPanel, wide ? 1 : 5);
        Grid.SetRowSpan(DetectedPanel, wide ? 4 : 1);
        DetectedPanel.Margin = wide ? new Thickness(12, 0, 0, 0) : new Thickness(0, 12, 0, 0);
    }

    public DashboardPage(DashboardViewModel viewModel, SteamTipViewModel steamTip)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        SteamTipBanner.DataContext = steamTip;
    }

    private void OnAssignClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DeviceItemViewModel device } anchor)
        {
            PlayerCard.OpenMenu(anchor, device.AssignOptions);
        }
    }

    private void OnDeviceMouseDown(object sender, MouseButtonEventArgs e) => _dragStart = e.GetPosition(null);

    private void OnDeviceMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var delta = e.GetPosition(null) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is { DataContext: DeviceItemViewModel device } item)
        {
            DragDrop.DoDragDrop(item, new DataObject(PlayerCard.DeviceDragFormat, device.Id.Value), DragDropEffects.Move);
        }
    }

    private void OnCardsDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(PlayerCard.DeviceDragFormat) is string id
            && FindAncestor<PlayerCard>(e.OriginalSource as DependencyObject) is { DataContext: PlayerCardViewModel card })
        {
            _viewModel.AssignDevice(new DeviceId(id), card.Index);
            e.Handled = true;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? element)
        where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match)
            {
                return match;
            }

            // Run and other content elements are not visuals.
            element = element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }

        return null;
    }
}
