using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Controlability.App.ViewModels;
using Controlability.Core.Models;

namespace Controlability.App.Controls;

/// <summary>
/// A vector drawing of a generic gamepad that shows a <see cref="LivePadViewModel"/>: pressed buttons light up, the stick
/// dots move inside circles that show the dead zone, and the trigger bars fill. It reads the state once per frame (at most
/// 60 Hz) and only while it is visible.
/// </summary>
public partial class ControllerView : UserControl
{
    public static readonly DependencyProperty LiveProperty =
        DependencyProperty.Register(nameof(Live), typeof(LivePadViewModel), typeof(ControllerView), new PropertyMetadata(null));

    /// <summary>False: show the raw controller. True: show the Xbox report after mapping.</summary>
    public static readonly DependencyProperty ShowOutputProperty =
        DependencyProperty.Register(nameof(ShowOutput), typeof(bool), typeof(ControllerView), new PropertyMetadata(false));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ControllerView), new PropertyMetadata("", OnTitleChanged));

    public static readonly DependencyProperty LeftInnerProperty = RegisterZone(nameof(LeftInner), 0.08);

    public static readonly DependencyProperty LeftOuterProperty = RegisterZone(nameof(LeftOuter), 0.98);

    public static readonly DependencyProperty RightInnerProperty = RegisterZone(nameof(RightInner), 0.08);

    public static readonly DependencyProperty RightOuterProperty = RegisterZone(nameof(RightOuter), 0.98);

    private const string IdleBrushKey = "ControlFillColorDefaultBrush";
    private const double TriggerWidth = 96;

    private static readonly XboxButton[] Buttons =
    [
        XboxButton.A, XboxButton.B, XboxButton.X, XboxButton.Y, XboxButton.LeftShoulder, XboxButton.RightShoulder,
        XboxButton.Back, XboxButton.Start, XboxButton.Guide, XboxButton.LeftThumb, XboxButton.RightThumb,
        XboxButton.DpadUp, XboxButton.DpadDown, XboxButton.DpadLeft, XboxButton.DpadRight,
    ];

    private readonly Dictionary<XboxButton, Shape> _shapes;
    private readonly StickParts _left;
    private readonly StickParts _right;
    private PadPose? _drawn;

    public ControllerView()
    {
        InitializeComponent();
        _shapes = new Dictionary<XboxButton, Shape>
        {
            [XboxButton.A] = BtnA,
            [XboxButton.B] = BtnB,
            [XboxButton.X] = BtnX,
            [XboxButton.Y] = BtnY,
            [XboxButton.LeftShoulder] = BtnLB,
            [XboxButton.RightShoulder] = BtnRB,
            [XboxButton.Back] = BtnBack,
            [XboxButton.Start] = BtnStart,
            [XboxButton.Guide] = BtnGuide,
            [XboxButton.LeftThumb] = LsWell,
            [XboxButton.RightThumb] = RsWell,
            [XboxButton.DpadUp] = DpadUp,
            [XboxButton.DpadDown] = DpadDown,
            [XboxButton.DpadLeft] = DpadLeft,
            [XboxButton.DpadRight] = DpadRight,
        };
        _left = new StickParts(LsWell, LsInner, LsOuter, LsDot);
        _right = new StickParts(RsWell, RsInner, RsOuter, RsDot);
        foreach (var dot in new[] { LsDot, RsDot })
        {
            dot.SetResourceReference(Shape.FillProperty, "TextFillColorPrimaryBrush");
        }

        LayoutRings();

        _ = new FrameTicker(this, Tick);
        Loaded += (_, _) =>
        {
            _drawn = null;
            Tick();
        };
    }

    public LivePadViewModel? Live
    {
        get => (LivePadViewModel?)GetValue(LiveProperty);
        set => SetValue(LiveProperty, value);
    }

    public bool ShowOutput
    {
        get => (bool)GetValue(ShowOutputProperty);
        set => SetValue(ShowOutputProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Inner dead zone of the left stick, 0..1: the dashed ring in the stick circle.</summary>
    public double LeftInner
    {
        get => (double)GetValue(LeftInnerProperty);
        set => SetValue(LeftInnerProperty, value);
    }

    public double LeftOuter
    {
        get => (double)GetValue(LeftOuterProperty);
        set => SetValue(LeftOuterProperty, value);
    }

    public double RightInner
    {
        get => (double)GetValue(RightInnerProperty);
        set => SetValue(RightInnerProperty, value);
    }

    public double RightOuter
    {
        get => (double)GetValue(RightOuterProperty);
        set => SetValue(RightOuterProperty, value);
    }

    private static DependencyProperty RegisterZone(string name, double fallback)
        => DependencyProperty.Register(name, typeof(double), typeof(ControllerView), new PropertyMetadata(fallback, (d, _) => ((ControllerView)d).LayoutRings()));

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (ControllerView)d;
        view.TitleText.Text = (string)e.NewValue;
        System.Windows.Automation.AutomationProperties.SetName(view, (string)e.NewValue);
    }

    private void Tick()
    {
        if (Live is not { } live)
        {
            return;
        }

        live.Poll();
        NoStateText.Visibility = live.HasState ? Visibility.Collapsed : Visibility.Visible;
        Draw(ShowOutput ? live.Output : live.Raw);
    }

    private void Draw(PadPose pose)
    {
        var previous = _drawn;
        if (previous == pose)
        {
            return;
        }

        var changed = previous is { } p ? p.Buttons ^ pose.Buttons : (XboxButton)ushort.MaxValue;
        foreach (var button in Buttons)
        {
            if ((changed & button) == 0)
            {
                continue;
            }

            var shape = _shapes[button];
            if ((pose.Buttons & button) != 0)
            {
                shape.Fill = AccentBrush();
            }
            else
            {
                shape.SetResourceReference(Shape.FillProperty, IdleBrushKey);
            }
        }

        MoveDot(_left, pose.LeftX, pose.LeftY);
        MoveDot(_right, pose.RightX, pose.RightY);
        LtFill.Width = TriggerWidth * pose.LeftTrigger;
        RtFill.Width = TriggerWidth * pose.RightTrigger;
        LtFill.Fill = AccentBrush();
        RtFill.Fill = AccentBrush();
        _drawn = pose;
    }

    private Brush AccentBrush() => TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DodgerBlue;

    private void LayoutRings()
    {
        if (_left is null || _right is null)
        {
            return;
        }

        PlaceRing(_left.Inner, _left.Well, LeftInner);
        PlaceRing(_left.Outer, _left.Well, LeftOuter);
        PlaceRing(_right.Inner, _right.Well, RightInner);
        PlaceRing(_right.Outer, _right.Well, RightOuter);
        MoveDot(_left, _drawn?.LeftX ?? 0, _drawn?.LeftY ?? 0);
        MoveDot(_right, _drawn?.RightX ?? 0, _drawn?.RightY ?? 0);
    }

    private static void PlaceRing(Ellipse ring, Ellipse well, double fraction)
    {
        var radius = well.Width / 2 * Math.Clamp(fraction, 0.01, 1);
        ring.Width = radius * 2;
        ring.Height = radius * 2;
        Canvas.SetLeft(ring, Canvas.GetLeft(well) + (well.Width / 2) - radius);
        Canvas.SetTop(ring, Canvas.GetTop(well) + (well.Height / 2) - radius);
    }

    private void MoveDot(StickParts stick, float x, float y)
    {
        var dot = stick.Dot;
        var travel = (stick.Well.Width / 2) - (dot.Width / 2);
        Canvas.SetLeft(dot, Canvas.GetLeft(stick.Well) + (stick.Well.Width / 2) - (dot.Width / 2) + (x * travel));
        Canvas.SetTop(dot, Canvas.GetTop(stick.Well) + (stick.Well.Height / 2) - (dot.Height / 2) - (y * travel));
    }

    private sealed record StickParts(Ellipse Well, Ellipse Inner, Ellipse Outer, Ellipse Dot);
}
