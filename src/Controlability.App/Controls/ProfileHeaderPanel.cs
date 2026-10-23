using System.Windows;
using System.Windows.Controls;

namespace Controlability.App.Controls;

/// <summary>Keeps profile actions at their natural width, stacking only when the editor is narrow.</summary>
public sealed class ProfileHeaderPanel : Panel
{
    private const double Gap = 18;
    private const double Breakpoint = 740;
    protected override Size MeasureOverride(Size available)
    {
        if (InternalChildren.Count != 2) return default;
        var field = InternalChildren[0]; var actions = InternalChildren[1];
        if (available.Width >= Breakpoint)
        {
            actions.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            field.Measure(new Size(Math.Max(0, available.Width - actions.DesiredSize.Width - Gap), double.PositiveInfinity));
            return new Size(available.Width, Math.Max(field.DesiredSize.Height, actions.DesiredSize.Height));
        }
        field.Measure(new Size(available.Width, double.PositiveInfinity)); actions.Measure(new Size(available.Width, double.PositiveInfinity));
        return new Size(available.Width, field.DesiredSize.Height + Gap / 2 + actions.DesiredSize.Height);
    }
    protected override Size ArrangeOverride(Size size)
    {
        if (InternalChildren.Count != 2) return size;
        var field = InternalChildren[0]; var actions = InternalChildren[1];
        if (size.Width >= Breakpoint)
        {
            var fieldWidth = Math.Max(0, size.Width - actions.DesiredSize.Width - Gap);
            field.Arrange(new Rect(0, 0, fieldWidth, size.Height)); actions.Arrange(new Rect(fieldWidth + Gap, 0, actions.DesiredSize.Width, size.Height));
        }
        else
        {
            field.Arrange(new Rect(0, 0, size.Width, field.DesiredSize.Height)); actions.Arrange(new Rect(0, field.DesiredSize.Height + Gap / 2, size.Width, actions.DesiredSize.Height));
        }
        return size;
    }
}
