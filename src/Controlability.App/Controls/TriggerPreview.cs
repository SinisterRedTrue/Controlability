using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Controlability.App.ViewModels;

namespace Controlability.App.Controls;

/// <summary>Physical and mapped trigger values from the existing profile preview pipeline.</summary>
public sealed class TriggerPreview : StackPanel
{
    public static readonly DependencyProperty LiveProperty = DependencyProperty.Register(
        nameof(Live), typeof(LivePadViewModel), typeof(TriggerPreview), new PropertyMetadata(null));

    private readonly ProgressBar[] _bars = new ProgressBar[4];
    private readonly TextBlock[] _values = new TextBlock[4];
    private readonly TextBlock _status = new();

    public LivePadViewModel? Live
    {
        get => (LivePadViewModel?)GetValue(LiveProperty);
        set => SetValue(LiveProperty, value);
    }

    public TriggerPreview()
    {
        string[] labels = ["LT · Physical input", "LT · Xbox output", "RT · Physical input", "RT · Xbox output"];
        for (var i = 0; i < labels.Length; i++)
        {
            Children.Add(new TextBlock { Text = labels[i], Margin = new Thickness(0, 12, 0, 4) });
            _bars[i] = new ProgressBar { Minimum = 0, Maximum = 1, Height = 12 };
            _bars[i].SetResourceReference(Control.ForegroundProperty, "BrandBrush");
            AutomationProperties.SetName(_bars[i], labels[i]);
            Children.Add(_bars[i]);
            _values[i] = new TextBlock { Margin = new Thickness(0, 4, 0, 4) };
            Children.Add(_values[i]);
        }
        _status.Margin = new Thickness(0, 12, 0, 0);
        _status.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        Children.Add(_status);
        _ = new FrameTicker(this, Tick);
    }

    private void Tick()
    {
        var live = Live;
        live?.Poll();
        float[] values = [live?.Raw.LeftTrigger ?? 0, live?.Output.LeftTrigger ?? 0,
            live?.Raw.RightTrigger ?? 0, live?.Output.RightTrigger ?? 0];
        for (var i = 0; i < values.Length; i++)
        {
            _bars[i].Value = values[i];
            _values[i].Text = values[i].ToString("P0");
        }
        _status.Text = live?.HasState == true ? "Live · profile preview" : Controlability.App.Resources.Strings.LiveNoState;
    }
}
