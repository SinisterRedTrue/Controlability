using Controlability.Core.Abstractions;
using Controlability.Core.Models;
using Controlability.Output.ViGEm;

namespace Controlability.Engine.Tests.Output;

public class ViGEmSinkTests
{
    private static XboxState Pressed(XboxButton button) => new() { Buttons = (ushort)button };

    [Fact]
    public void Submit_SendsFirstStateEvenWhenNeutral()
    {
        var (sink, factory) = Create();
        var slot = sink.Connect(0);

        sink.SubmitIfChanged(slot, XboxState.Neutral).Should().BeTrue();

        factory.Targets[0].Submitted.Should().HaveCount(1);
    }

    [Fact]
    public void Submit_SkipsUnchangedState()
    {
        var (sink, factory) = Create();
        var slot = sink.Connect(0);
        var state = Pressed(XboxButton.A);

        sink.SubmitIfChanged(slot, state).Should().BeTrue();
        sink.SubmitIfChanged(slot, state).Should().BeFalse();
        sink.Submit(slot, state);

        factory.Targets[0].Submitted.Should().HaveCount(1);
    }

    [Fact]
    public void Submit_SendsAgainWhenAnyFieldChanges()
    {
        var (sink, factory) = Create();
        var slot = sink.Connect(0);
        var state = Pressed(XboxButton.A);
        sink.Submit(slot, state);

        state.LeftTrigger = 1;
        sink.Submit(slot, state);
        state.RightY = -1;
        sink.Submit(slot, state);
        state.Buttons = 0;
        sink.Submit(slot, state);

        factory.Targets[0].Submitted.Should().HaveCount(4);
    }

    [Fact]
    public void Submit_TracksEachSlotSeparately()
    {
        var (sink, factory) = Create();
        var first = sink.Connect(0);
        var second = sink.Connect(1);
        var state = Pressed(XboxButton.B);

        sink.Submit(first, state);
        sink.Submit(second, state);
        sink.Submit(first, state);

        factory.Targets[0].Submitted.Should().HaveCount(1);
        factory.Targets[1].Submitted.Should().HaveCount(1);
    }

    [Fact]
    public void Reconnect_SendsTheFirstStateAgain()
    {
        var (sink, factory) = Create();
        var slot = sink.Connect(0);
        sink.Submit(slot, Pressed(XboxButton.A));
        sink.Disconnect(slot);

        slot = sink.Connect(0);
        sink.Submit(slot, Pressed(XboxButton.A));

        factory.Targets.Should().HaveCount(2);
        factory.Targets[1].Submitted.Should().HaveCount(1);
    }

    [Fact]
    public void Unavailable_EverythingIsASafeNoOp()
    {
        var factory = new FakeFactory { Available = false };
        using var sink = new ViGEmSink(factory);

        sink.IsAvailable.Should().BeFalse();
        var slot = sink.Connect(0);
        slot.IsValid.Should().BeFalse();
        sink.Submit(slot, Pressed(XboxButton.A));
        sink.GetXInputUserIndex(slot).Should().BeNull();
        sink.Disconnect(slot);

        factory.Targets.Should().BeEmpty();
    }

    [Fact]
    public void Connect_StoresUserIndexOnceReported()
    {
        var (sink, factory) = Create();
        var slot = sink.Connect(0);

        sink.GetXInputUserIndex(slot).Should().BeNull();
        factory.Targets[0].UserIndex = 2;

        sink.GetXInputUserIndex(slot).Should().Be(2);
    }

    [Fact]
    public void Feedback_RaisesRumbleRequestedForTheSlot()
    {
        var (sink, factory) = Create();
        sink.Connect(0);
        var slot = sink.Connect(1);
        var received = new List<(SlotHandle Slot, byte Large, byte Small)>();
        sink.RumbleRequested += (s, large, small) => received.Add((s, large, small));

        factory.Targets[1].RaiseFeedback(200, 50);

        received.Should().Equal((slot, (byte)200, (byte)50));
    }

    [Fact]
    public void Dispose_DisconnectsEveryPad()
    {
        var (sink, factory) = Create();
        sink.Connect(0);
        sink.Connect(1);

        sink.Dispose();

        factory.Targets.Should().OnlyContain(t => t.Disposed && !t.Connected);
        factory.Disposed.Should().BeTrue();
    }

    private static (ViGEmSink Sink, FakeFactory Factory) Create()
    {
        var factory = new FakeFactory();
        return (new ViGEmSink(factory), factory);
    }

    private sealed class FakeFactory : IXboxTargetFactory
    {
        public bool Available { get; init; } = true;

        public bool Disposed { get; private set; }

        public List<FakeTarget> Targets { get; } = [];

        public bool IsAvailable => Available;

        public IXboxTarget Create()
        {
            var target = new FakeTarget();
            Targets.Add(target);
            return target;
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeTarget : IXboxTarget
    {
        public event Action<byte, byte>? Feedback;

        public List<XboxState> Submitted { get; } = [];

        public bool Connected { get; private set; }

        public bool Disposed { get; private set; }

        public int? UserIndex { get; set; }

        public void Connect() => Connected = true;

        public void Disconnect() => Connected = false;

        public void Submit(in XboxState state) => Submitted.Add(state);

        public void RaiseFeedback(byte large, byte small) => Feedback?.Invoke(large, small);

        public void Dispose()
        {
            Disconnect();
            Disposed = true;
        }
    }
}
