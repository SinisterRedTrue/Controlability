using Controlability.Core.Models;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Nefarius.ViGEm.Client.Targets.Xbox360.Exceptions;

namespace Controlability.Output.ViGEm;

internal sealed class ViGEmXboxTarget : IXboxTarget
{
    private readonly IXbox360Controller _pad;
    private bool _connected;

    public ViGEmXboxTarget(ViGEmClient client)
    {
        _pad = client.CreateXbox360Controller();
        _pad.AutoSubmitReport = false;
        _pad.FeedbackReceived += OnFeedback;
    }

    public event Action<byte, byte>? Feedback;

    public int? UserIndex
    {
        get
        {
            if (!_connected)
            {
                return null;
            }

            try
            {
                return _pad.UserIndex;
            }
            catch (Xbox360UserIndexNotReportedException)
            {
                return null;
            }
        }
    }

    public void Connect()
    {
        _pad.Connect();
        _connected = true;
    }

    public void Disconnect()
    {
        if (!_connected)
        {
            return;
        }

        _connected = false;
        _pad.Disconnect();
    }

    public void Submit(in XboxState state)
    {
        _pad.SetButtonsFull(state.Buttons);
        _pad.SetAxisValue(Xbox360Axis.LeftThumbX, state.LeftX);
        _pad.SetAxisValue(Xbox360Axis.LeftThumbY, state.LeftY);
        _pad.SetAxisValue(Xbox360Axis.RightThumbX, state.RightX);
        _pad.SetAxisValue(Xbox360Axis.RightThumbY, state.RightY);
        _pad.SetSliderValue(Xbox360Slider.LeftTrigger, state.LeftTrigger);
        _pad.SetSliderValue(Xbox360Slider.RightTrigger, state.RightTrigger);
        _pad.SubmitReport();
    }

    public void Dispose()
    {
        _pad.FeedbackReceived -= OnFeedback;
        Disconnect();
        (_pad as IDisposable)?.Dispose();
    }

    private void OnFeedback(object sender, Xbox360FeedbackReceivedEventArgs e)
        => Feedback?.Invoke(e.LargeMotor, e.SmallMotor);
}
