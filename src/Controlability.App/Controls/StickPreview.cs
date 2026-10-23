using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Controlability.App.ViewModels;

namespace Controlability.App.Controls;

/// <summary>
/// One stick circle with the dead zone rings: a ring for the stick as it is and an accent dot for what the game gets
/// (<see cref="LivePadViewModel.Output"/>, which uses the unsaved profile values when the live model has a preview mapping).
/// </summary>
public sealed class StickPreview : Canvas
{
    public static readonly DependencyProperty LiveProperty =
        DependencyProperty.Register(nameof(Live), typeof(LivePadViewModel), typeof(StickPreview), new PropertyMetadata(null));

    public static readonly DependencyProperty IsRightProperty =
        DependencyProperty.Register(nameof(IsRight), typeof(bool), typeof(StickPreview), new PropertyMetadata(false));

    public static readonly DependencyProperty InnerProperty =
        DependencyProperty.Register(nameof(Inner), typeof(double), typeof(StickPreview), new PropertyMetadata(0.08, OnZoneChanged));

    public static readonly DependencyProperty OuterProperty =
        DependencyProperty.Register(nameof(Outer), typeof(double), typeof(StickPreview), new PropertyMetadata(0.98, OnZoneChanged));

    private const double Size = 180;
    private const double Radius = 86;
    private const double DotSize = 10;

    private readonly Ellipse _well = new() { Width = Radius * 2, Height = Radius * 2, StrokeThickness = 1.5 };
    private readonly Ellipse _inner = new() { StrokeThickness = 1, StrokeDashArray = [2, 2], Fill = Brushes.Transparent };
    private readonly Ellipse _outer = new() { StrokeThickness = 1, StrokeDashArray = [2, 2], Fill = Brushes.Transparent };
    private readonly Ellipse _rawDot = new() { Width = DotSize + 6, Height = DotSize + 6, Fill = Brushes.Transparent, StrokeThickness = 1.5 };
    private readonly Ellipse _outputDot = new() { Width = DotSize, Height = DotSize };

    public StickPreview()
    {
        Width = Size;
        Height = Size;
        _well.SetResourceReference(Shape.FillProperty, "ControlFillColorDefaultBrush");
        _well.SetResourceReference(Shape.StrokeProperty, "TextFillColorSecondaryBrush");
        _inner.SetResourceReference(Shape.StrokeProperty, "TextFillColorTertiaryBrush");
        _outer.SetResourceReference(Shape.StrokeProperty, "TextFillColorTertiaryBrush");
        _rawDot.SetResourceReference(Shape.StrokeProperty, "TextFillColorPrimaryBrush");
        _outputDot.SetResourceReference(Shape.FillProperty, "AccentFillColorDefaultBrush");
        foreach (var shape in new Shape[] { _well, _inner, _outer, _rawDot, _outputDot })
        {
            shape.IsHitTestVisible = false;
            Children.Add(shape);
        }

        Place(_well, 0, 0, Radius);
        LayoutRings();
        MoveDot(_rawDot, 0, 0);
        MoveDot(_outputDot, 0, 0);
        _ = new FrameTicker(this, Tick);
    }

    public LivePadViewModel? Live
    {
        get => (LivePadViewModel?)GetValue(LiveProperty);
        set => SetValue(LiveProperty, value);
    }

    public bool IsRight
    {
        get => (bool)GetValue(IsRightProperty);
        set => SetValue(IsRightProperty, value);
    }

    public double Inner
    {
        get => (double)GetValue(InnerProperty);
        set => SetValue(InnerProperty, value);
    }

    public double Outer
    {
        get => (double)GetValue(OuterProperty);
        set => SetValue(OuterProperty, value);
    }

    private static void OnZoneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((StickPreview)d).LayoutRings();

    private void Tick()
    {
        if (Live is not { } live)
        {
            return;
        }

        live.Poll();
        var raw = live.Raw;
        var output = live.Output;
        MoveDot(_rawDot, IsRight ? raw.RightX : raw.LeftX, IsRight ? raw.RightY : raw.LeftY);
        MoveDot(_outputDot, IsRight ? output.RightX : output.LeftX, IsRight ? output.RightY : output.LeftY);
    }

    private void LayoutRings()
    {
        Place(_inner, 0, 0, Radius * Math.Clamp(Inner, 0.01, 1));
        Place(_outer, 0, 0, Radius * Math.Clamp(Outer, 0.01, 1));
    }

    private static void Place(Ellipse ellipse, double offsetX, double offsetY, double radius)
    {
        ellipse.Width = radius * 2;
        ellipse.Height = radius * 2;
        SetLeft(ellipse, (Size / 2) - radius + offsetX);
        SetTop(ellipse, (Size / 2) - radius + offsetY);
    }

    private static void MoveDot(Ellipse dot, double x, double y)
    {
        var travel = Radius - (DotSize / 2);
        SetLeft(dot, (Size / 2) - (dot.Width / 2) + (x * travel));
        SetTop(dot, (Size / 2) - (dot.Height / 2) - (y * travel));
    }
}
