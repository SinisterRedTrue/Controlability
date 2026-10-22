using Controlability.Core.Mapping;
using Controlability.Core.Models;
using Controlability.Engine;

namespace Controlability.App.ViewModels;

/// <summary>
/// The live state of one device for the test panels. <see cref="Poll"/> reads the engine's latest values; the panels call
/// it once per frame. With <see cref="PreviewMapping"/> set, the Xbox output is computed from the raw input with those
/// (unsaved) settings instead of read from the engine.
/// </summary>
public sealed class LivePadViewModel(IEngine engine)
{
    public DeviceId? Device { get; set; }

    /// <summary>Mapping to show instead of the engine's, for a profile that is being edited.</summary>
    public Func<MappingSettings?>? PreviewMapping { get; set; }

    public bool HasState { get; private set; }

    public InputState RawState { get; private set; }

    public PadPose Raw { get; private set; }

    public PadPose Output { get; private set; }

    /// <summary>Reads the latest state. Returns whether anything changed since the last call.</summary>
    public bool Poll()
    {
        if (Device is not { } device || !engine.TryGetLiveState(device, out var raw, out var mapped))
        {
            var changed = HasState;
            HasState = false;
            RawState = default;
            Raw = default;
            Output = default;
            return changed;
        }

        if (PreviewMapping?.Invoke() is { } mapping)
        {
            mapped = Mapper.Map(in raw, mapping);
        }

        var rawPose = PadPose.FromRaw(in raw);
        var outputPose = PadPose.FromXbox(in mapped);
        var different = !HasState || rawPose != Raw || outputPose != Output;
        HasState = true;
        RawState = raw;
        Raw = rawPose;
        Output = outputPose;
        return different;
    }
}
