using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
