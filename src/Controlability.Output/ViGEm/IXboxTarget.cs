using Controlability.Core.Models;

namespace Controlability.Output.ViGEm;

/// <summary>One virtual Xbox 360 pad. Exists so <see cref="ViGEmSink"/> can be tested without the driver.</summary>
internal interface IXboxTarget : IDisposable
{
    /// <summary>Game rumble: large motor, small motor.</summary>
    event Action<byte, byte>? Feedback;

    /// <summary>XInput user index 0..3, or null while the driver has not reported it yet.</summary>
    int? UserIndex { get; }

    void Connect();

    void Disconnect();

    /// <summary>Writes every field of the report and sends it once.</summary>
    void Submit(in XboxState state);
}

/// <summary>Creates <see cref="IXboxTarget"/> instances; <see cref="IsAvailable"/> is false when ViGEmBus is missing.</summary>
internal interface IXboxTargetFactory : IDisposable
{
    bool IsAvailable { get; }

    IXboxTarget Create();
}
