using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Controlability.App.ViewModels;
using Controlability.Core.Models;

namespace Controlability.App.Controls;

/// <summary>Which pad layout <see cref="PadDiagram"/> draws.</summary>
public enum PadKind
{
    Xbox,
    PlayStation,
}

/// <summary>
/// A drawing of a PlayStation or Xbox pad that shows a <see cref="LivePadViewModel"/>: pressed buttons light up, the stick
/// dots move inside their wells, and the trigger bars beside the pad fill with their percentage. It reads the state once per
/// frame (at most 60 Hz) and only while it is visible.
/// </summary>
public partial class PadDiagram : UserControl
{
    public static readonly DependencyProperty LiveProperty =
        DependencyProperty.Register(nameof(Live), typeof(LivePadViewModel), typeof(PadDiagram), new PropertyMetadata(null, (d, _) => ((PadDiagram)d).RefreshDrawing()));

    /// <summary>False: show the raw controller. True: show the Xbox report after mapping.</summary>
    public static readonly DependencyProperty ShowOutputProperty =
        DependencyProperty.Register(nameof(ShowOutput), typeof(bool), typeof(PadDiagram), new PropertyMetadata(false, (d, _) => ((PadDiagram)d).RefreshDrawing()));

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(PadKind), typeof(PadDiagram), new PropertyMetadata(PadKind.Xbox, (d, _) => ((PadDiagram)d).ApplyKind()));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PadDiagram), new PropertyMetadata("", (d, e) => ((PadDiagram)d).TitleText.Text = (string)e.NewValue));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(PadDiagram), new PropertyMetadata("", (d, e) => ((PadDiagram)d).SubtitleText.Text = (string)e.NewValue));

    public static readonly DependencyProperty LeftInnerProperty = RegisterZone(nameof(LeftInner), 0.08);

    public static readonly DependencyProperty LeftOuterProperty = RegisterZone(nameof(LeftOuter), 0.98);

    public static readonly DependencyProperty RightInnerProperty = RegisterZone(nameof(RightInner), 0.08);

    public static readonly DependencyProperty RightOuterProperty = RegisterZone(nameof(RightOuter), 0.98);

    private readonly Layout _playStation;
    private readonly Layout _xbox;
    private Layout _active;
    private PadPose? _drawn;
    private List<Bar> _bars = [];

    public PadDiagram()
    {
        InitializeComponent();
        _playStation = new Layout(
            PsCanvas,
            new Dictionary<XboxButton, Shape[]>
            {
                [XboxButton.A] = [PsCross],
                [XboxButton.B] = [PsCircle],
                [XboxButton.X] = [PsSquare],
                [XboxButton.Y] = [PsTriangle],
                [XboxButton.Back] = [PsShare],
                [XboxButton.Start] = [PsOptions],
                [XboxButton.Guide] = [PsHome],
                [XboxButton.LeftShoulder] = [PsLb],
                [XboxButton.RightShoulder] = [PsRb],
                [XboxButton.LeftThumb] = [PsLsWell, PsLsCap],
                [XboxButton.RightThumb] = [PsRsWell, PsRsCap],
                [XboxButton.DpadUp] = [PsDpadUp],
                [XboxButton.DpadDown] = [PsDpadDown],
                [XboxButton.DpadLeft] = [PsDpadLeft],
                [XboxButton.DpadRight] = [PsDpadRight],
            },
            new StickParts(PsLsWell, PsLsInner, PsLsOuter, PsLsDot),
            new StickParts(PsRsWell, PsRsInner, PsRsOuter, PsRsDot),
            PsLeftText,
            PsRightText);
        _xbox = new Layout(
            XbCanvas,
            new Dictionary<XboxButton, Shape[]>
            {
                [XboxButton.A] = [XbA],
                [XboxButton.B] = [XbB],
                [XboxButton.X] = [XbX],
                [XboxButton.Y] = [XbY],
                [XboxButton.LeftShoulder] = [XbLb],
                [XboxButton.RightShoulder] = [XbRb],
                [XboxButton.Back] = [XbBack],
                [XboxButton.Start] = [XbStart],
                [XboxButton.Guide] = [XbGuide],
                [XboxButton.LeftThumb] = [XbLsWell, XbLsCap],
                [XboxButton.RightThumb] = [XbRsWell, XbRsCap],
                [XboxButton.DpadUp] = [XbDpadUp],
                [XboxButton.DpadDown] = [XbDpadDown],
                [XboxButton.DpadLeft] = [XbDpadLeft],
                [XboxButton.DpadRight] = [XbDpadRight],
            },
            new StickParts(XbLsWell, XbLsInner, XbLsOuter, XbLsDot),
            new StickParts(XbRsWell, XbRsInner, XbRsOuter, XbRsDot),
            XbLeftText,
            XbRightText);
        foreach (var dot in new[] { PsLsDot, PsRsDot, XbLsDot, XbRsDot })
        {
            dot.SetResourceReference(Shape.FillProperty, "TextFillColorPrimaryBrush");
        }

        _active = _xbox;
        ApplyKind();

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

    public PadKind Kind
    {
        get => (PadKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The device name in brackets, beside the title.</summary>
    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Inner dead zone of the left stick, 0..1: the faint ring in the stick circle.</summary>
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
        => DependencyProperty.Register(name, typeof(double), typeof(PadDiagram), new PropertyMetadata(fallback, (d, _) => ((PadDiagram)d).LayoutRings()));

    private void ApplyKind()
    {
        if (_playStation is null || _xbox is null)
        {
            return;
        }

        _active = Kind == PadKind.PlayStation ? _playStation : _xbox;
        _playStation.Canvas.Visibility = _active == _playStation ? Visibility.Visible : Visibility.Collapsed;
        _xbox.Canvas.Visibility = _active == _xbox ? Visibility.Visible : Visibility.Collapsed;

        LeftBars.Children.Clear();
        RightBars.Children.Clear();
        _bars = [];
        var playStation = Kind == PadKind.PlayStation;
        // The physical DS4 shows both analog triggers (L2/R2) in its left-hand pair.
        // All four mapped Xbox controls belong to the output panel: LT/LB left, RB/RT right.
        AddBar(LeftBars, playStation ? "L2" : "LT", BarInput.LeftTrigger, isLeft: true);
        AddBar(LeftBars, playStation ? "R2" : "LB", playStation ? BarInput.RightTrigger : BarInput.LeftShoulder, isLeft: true);
        if (!playStation)
        {
            AddBar(RightBars, "RB", BarInput.RightShoulder, isLeft: false);
            AddBar(RightBars, "RT", BarInput.RightTrigger, isLeft: false);
        }

        RightBars.Visibility = playStation ? Visibility.Collapsed : Visibility.Visible;
        DiagramLayout.Width = playStation ? 590 : 596;
        DiagramHeading.Margin = new Thickness(playStation ? 50 : 0, 0, 0, 15);
        BarsColumn.Width = new GridLength(132);
        RightBarsColumn.Width = new GridLength(playStation ? 64 : 118);
        LeftBars.Margin = new Thickness(18, 14, 0, 0);
        RightBars.Margin = new Thickness(6, 14, 0, 0);
        LayoutRings();
        _drawn = null;
        Tick();
    }

    private void AddBar(Panel host, string label, BarInput input, bool isLeft)
    {
        var bar = new Bar(label, input, isLeft);
        _bars.Add(bar);
        host.Children.Add(bar.Root);
    }

    private void RefreshDrawing()
    {
        if (_active is null) return;
        _drawn = null;
        Tick();
    }

    private void Tick()
    {
        if (Live is not { } live)
        {
            Draw(default);
            return;
        }

        live.Poll();
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
        foreach (var (button, shapes) in _active.Buttons)
        {
            if ((changed & button) == 0)
            {
                continue;
            }

            foreach (var shape in shapes)
            {
                if ((pose.Buttons & button) != 0)
                {
                    shape.Fill = AccentBrush();
                }
                else
                {
                    shape.ClearValue(Shape.FillProperty);
                }
            }
        }

        MoveDot(_active.Left, pose.LeftX, pose.LeftY);
        MoveDot(_active.Right, pose.RightX, pose.RightY);
        // The Xbox reference shows smooth stick caps at rest. Keep the moving indicator for actual travel.
        XbLsDot.Opacity = Math.Abs(pose.LeftX) + Math.Abs(pose.LeftY) > 0.001 ? 1 : 0;
        XbRsDot.Opacity = Math.Abs(pose.RightX) + Math.Abs(pose.RightY) > 0.001 ? 1 : 0;
        _active.LeftText.Text = FormatStick("L", pose.LeftX, pose.LeftY);
        _active.RightText.Text = FormatStick("R", pose.RightX, pose.RightY);
        foreach (var bar in _bars)
        {
            bar.Show(pose, AccentBrush());
        }

        _drawn = pose;
    }

    private static string FormatStick(string side, float x, float y)
        => string.Create(CultureInfo.InvariantCulture, $"{side} {x:0.00}, {y:0.00}");

    private Brush AccentBrush() => TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DodgerBlue;

    private void LayoutRings()
    {
        if (_playStation is null || _xbox is null)
        {
            return;
        }

        foreach (var layout in new[] { _playStation, _xbox })
        {
            PlaceRing(layout.Left.Inner, layout.Left.Well, LeftInner);
            PlaceRing(layout.Left.Outer, layout.Left.Well, LeftOuter);
            PlaceRing(layout.Right.Inner, layout.Right.Well, RightInner);
            PlaceRing(layout.Right.Outer, layout.Right.Well, RightOuter);
            MoveDot(layout.Left, _drawn?.LeftX ?? 0, _drawn?.LeftY ?? 0);
            MoveDot(layout.Right, _drawn?.RightX ?? 0, _drawn?.RightY ?? 0);
        }
    }

    private static void PlaceRing(Ellipse ring, Ellipse well, double fraction)
    {
        var radius = well.Width / 2 * Math.Clamp(fraction, 0.01, 1);
        ring.Width = radius * 2;
        ring.Height = radius * 2;
        Canvas.SetLeft(ring, Canvas.GetLeft(well) + (well.Width / 2) - radius);
        Canvas.SetTop(ring, Canvas.GetTop(well) + (well.Height / 2) - radius);
    }

    private static void MoveDot(StickParts stick, float x, float y)
    {
        var dot = stick.Dot;
        var travel = (stick.Well.Width / 2) - (dot.Width / 2);
        Canvas.SetLeft(dot, Canvas.GetLeft(stick.Well) + (stick.Well.Width / 2) - (dot.Width / 2) + (x * travel));
        Canvas.SetTop(dot, Canvas.GetTop(stick.Well) + (stick.Well.Height / 2) - (dot.Height / 2) - (y * travel));
    }

    private sealed record StickParts(Ellipse Well, Ellipse Inner, Ellipse Outer, Ellipse Dot);

    private sealed record Layout(
        Canvas Canvas,
        Dictionary<XboxButton, Shape[]> Buttons,
        StickParts Left,
        StickParts Right,
        TextBlock LeftText,
        TextBlock RightText);

    private enum BarInput { LeftTrigger, RightTrigger, LeftShoulder, RightShoulder }

    /// <summary>A tall bar beside the pad: a trigger that fills with its travel, or a shoulder that is on or off.</summary>
    private sealed class Bar
    {
        private const double Height = 112;

        private readonly BarInput _input;
        private readonly string _label;
        private readonly Border _fill;
        private readonly TextBlock _percent;

        public Bar(string label, BarInput input, bool isLeft)
        {
            _input = input;
            _label = label;
            _fill = new Border { Height = 0, VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(3), IsHitTestVisible = false };
            var frame = new Border
            {
                Width = 32,
                Height = Height,
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                Child = _fill,
            };
            frame.SetResourceReference(Border.BorderBrushProperty, "TextFillColorSecondaryBrush");
            frame.SetResourceReference(Border.BackgroundProperty, "DeviceTriggerBrush");
            System.Windows.Automation.AutomationProperties.SetName(frame, label);
            var title = new TextBlock { Text = label, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
            title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            _percent = new TextBlock { Text = "0%", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
            _percent.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            var stack = new StackPanel { Margin = new Thickness(0, 0, isLeft ? 24 : 0, 0) };
            if (!isLeft)
            {
                stack.Margin = new Thickness(24, 0, 0, 0);
            }

            stack.Children.Add(title);
            stack.Children.Add(frame);
            stack.Children.Add(_percent);
            Root = stack;
        }

        public FrameworkElement Root { get; }

        public void Show(PadPose pose, Brush accent)
        {
            var value = _input switch
            {
                BarInput.LeftTrigger => Math.Clamp(pose.LeftTrigger, 0, 1),
                BarInput.RightTrigger => Math.Clamp(pose.RightTrigger, 0, 1),
                BarInput.LeftShoulder => (pose.Buttons & XboxButton.LeftShoulder) != 0 ? 1.0 : 0.0,
                BarInput.RightShoulder => (pose.Buttons & XboxButton.RightShoulder) != 0 ? 1.0 : 0.0,
                _ => 0.0,
            };
            _fill.Height = (Height - 3) * value;
            _fill.Background = accent;
            _percent.Text = string.Create(CultureInfo.InvariantCulture, $"{Math.Round(value * 100):0}%");
            System.Windows.Automation.AutomationProperties.SetHelpText(Root, $"{_label}: {_percent.Text}");
        }
    }
}
