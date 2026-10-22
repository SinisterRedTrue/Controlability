using System.Windows;
using System.Windows.Media;

namespace Controlability.App.Controls;

/// <summary>
/// Calls <c>tick</c> from <see cref="CompositionTarget.Rendering"/> at most 60 times a second, and only while the owner is
/// loaded and visible, so a page that is not on screen costs nothing.
/// </summary>
internal sealed class FrameTicker
{
    /// <summary>A bit under 1/60 s, so a 60 Hz display is not thinned to 30 Hz by timing jitter.</summary>
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(15.5);

    private readonly FrameworkElement _owner;
    private readonly Action _tick;
    private TimeSpan _last = TimeSpan.MinValue;
    private bool _running;

    public FrameTicker(FrameworkElement owner, Action tick)
    {
        _owner = owner;
        _tick = tick;
        owner.Loaded += (_, _) => Update();
        owner.Unloaded += (_, _) => Update();
        owner.IsVisibleChanged += (_, _) => Update();
    }

    private void Update()
    {
        var shouldRun = _owner.IsLoaded && _owner.IsVisible;
        if (shouldRun == _running)
        {
            return;
        }

        _running = shouldRun;
        if (shouldRun)
        {
            _last = TimeSpan.MinValue;
            CompositionTarget.Rendering += OnRendering;
        }
        else
        {
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (_last != TimeSpan.MinValue && now - _last < MinInterval)
        {
            return;
        }

        _last = now;
        _tick();
    }
}
