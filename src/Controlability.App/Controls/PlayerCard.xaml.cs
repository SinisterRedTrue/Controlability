using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Controlability.App.Resources;
using Controlability.App.ViewModels;

namespace Controlability.App.Controls;

/// <summary>A player slot. Highlights while a device is dragged over it; the dashboard page handles the drop.</summary>
public partial class PlayerCard : UserControl
{
    /// <summary>Data format of a dragged device: its <c>DeviceId</c> value as text.</summary>
    public const string DeviceDragFormat = "Controlability.DeviceId";

    public PlayerCard()
    {
        InitializeComponent();
    }

    /// <summary>Opens a pop-up menu under <paramref name="anchor"/>; the items keep the app's menu look.</summary>
    internal static void OpenMenu(FrameworkElement anchor, IEnumerable<object> items)
    {
        var style = new Style(typeof(MenuItem), (Style?)anchor.TryFindResource(typeof(MenuItem)));
        style.Setters.Add(new Setter(MenuItem.HeaderProperty, new Binding("Header")));
        style.Setters.Add(new Setter(MenuItem.CommandProperty, new Binding("Command")));
        style.Setters.Add(new Setter(MenuItem.IsCheckedProperty, new Binding("IsChecked") { Mode = BindingMode.OneWay }));
        new ContextMenu
        {
            PlacementTarget = anchor,
            Placement = PlacementMode.Bottom,
            MinWidth = anchor.ActualWidth,
            ItemsSource = items.ToList(),
            ItemContainerStyle = style,
            IsOpen = true,
        };
    }

    private void OnAssignClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement anchor && DataContext is PlayerCardViewModel card)
        {
            OpenMenu(anchor, card.AssignOptions);
        }
    }

    private void OnProfileClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement anchor && DataContext is PlayerCardViewModel card)
        {
            OpenMenu(anchor, card.ProfileOptions);
        }
    }

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement anchor || DataContext is not PlayerCardViewModel card)
        {
            return;
        }

        new ContextMenu
        {
            PlacementTarget = anchor,
            Placement = PlacementMode.Bottom,
            Items =
            {
                new MenuItem { Header = Strings.MenuIdentify, Command = card.IdentifyCommand },
                new MenuItem { Header = Strings.MenuUnassign, Command = card.UnassignCommand },
            },
            IsOpen = true,
        };
    }

    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        if (e.Data.GetDataPresent(DeviceDragFormat))
        {
            Root.BorderBrush = TryFindResource("SystemAccentColorPrimaryBrush") as Brush ?? Brushes.DodgerBlue;
            Root.BorderThickness = new Thickness(2);
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        e.Effects = e.Data.GetDataPresent(DeviceDragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        base.OnDragLeave(e);
        ResetHighlight();
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        ResetHighlight();
    }

    private void ResetHighlight()
    {
        Root.ClearValue(Border.BorderBrushProperty);
        Root.ClearValue(Border.BorderThicknessProperty);
    }
}
