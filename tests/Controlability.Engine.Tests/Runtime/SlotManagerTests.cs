using Controlability.Core.Models;
using Controlability.Core.Settings;

namespace Controlability.Engine.Tests.Runtime;

public class SlotManagerTests
{
    private static readonly DeviceId A = new("A");
    private static readonly DeviceId B = new("B");
    private static readonly DeviceId C = new("C");

    private readonly FakeClock _clock = new();

    private SlotManager Create(SlotPolicy policy = SlotPolicy.Auto, Dictionary<string, int>? remembered = null)
        => new(_clock, policy, TimeSpan.FromSeconds(10), remembered);

    [Fact]
    public void Auto_AssignsLowestFreeSlotInOrder()
    {
        var slots = Create();

        slots.OnDeviceAdded(A);
        slots.OnDeviceAdded(B);
        slots.OnDeviceAdded(C);

        slots.FindSlot(A).Should().Be(0);
        slots.FindSlot(B).Should().Be(1);
        slots.FindSlot(C).Should().Be(2);
    }

    [Fact]
    public void Auto_FifthDeviceStaysUnassigned()
    {
        var slots = Create();
        foreach (var name in "ABCD")
        {
            slots.OnDeviceAdded(new DeviceId(name.ToString()));
        }

        var events = slots.OnDeviceAdded(new DeviceId("E"));

        events.Should().BeEmpty();
        slots.FindSlot(new DeviceId("E")).Should().BeNull();
    }

    [Fact]
    public void Auto_ReusesSlotFreedByUnassign()
    {
        var slots = Create();
        slots.OnDeviceAdded(A);
        slots.OnDeviceAdded(B);
        slots.Unassign(0);

        slots.OnDeviceAdded(C);

        slots.FindSlot(C).Should().Be(0);
    }

    [Fact]
    public void Remember_GivesKnownDeviceItsLastSlot()
    {
        var slots = Create(SlotPolicy.Remember, new() { ["B"] = 2 });

        slots.OnDeviceAdded(A);
        slots.OnDeviceAdded(B);

        slots.FindSlot(A).Should().Be(0);
        slots.FindSlot(B).Should().Be(2);
    }

    [Fact]
    public void Remember_FallsBackToLowestFreeWhenRememberedSlotIsTaken()
    {
        var slots = Create(SlotPolicy.Remember, new() { ["B"] = 0 });
        slots.OnDeviceAdded(A);

        slots.OnDeviceAdded(B);

        slots.FindSlot(B).Should().Be(1);
    }

    [Fact]
    public void Auto_IgnoresRememberedSlots()
    {
        var slots = Create(SlotPolicy.Auto, new() { ["B"] = 2 });

        slots.OnDeviceAdded(B);

        slots.FindSlot(B).Should().Be(0);
    }

    [Fact]
    public void Assignment_IsRememberedAndAnnounced()
    {
        var slots = Create(SlotPolicy.Remember);
        var raised = 0;
        slots.RememberedChanged += () => raised++;

        slots.OnDeviceAdded(A);
        slots.Assign(A, 3);

        slots.Remembered["A"].Should().Be(3);
        raised.Should().Be(2);
    }

    [Fact]
    public void Manual_DoesNotAssignOnConnect()
    {
        var slots = Create(SlotPolicy.Manual);

        var events = slots.OnDeviceAdded(A);

        events.Should().BeEmpty();
        slots.FindSlot(A).Should().BeNull();
        slots.Assign(A, 1).Should().ContainSingle(e => e.Kind == SlotEventKind.Assigned && e.Slot == 1);
    }

    [Fact]
    public void Assign_MovesDeviceAndEmptiesItsOldSlot()
    {
        var slots = Create();
        slots.OnDeviceAdded(A);

        var events = slots.Assign(A, 2);

        events.Select(e => (e.Kind, e.Slot)).Should().Equal((SlotEventKind.Emptied, 0), (SlotEventKind.Assigned, 2));
        slots.GetState(0).Should().Be(SlotState.Empty);
        slots.GetState(2).Should().Be(SlotState.Active);
    }

    [Fact]
    public void Assign_OfUnknownDevice_DoesNothing()
    {
        Create().Assign(A, 0).Should().BeEmpty();
    }

    [Fact]
    public void Removal_StartsGracePeriodAndKeepsTheSlotReserved()
    {
        var slots = Create();
        slots.OnDeviceAdded(A);

        var events = slots.OnDeviceRemoved(A);
        slots.OnDeviceAdded(B);

        events.Should().ContainSingle(e => e.Kind == SlotEventKind.Waiting && e.Slot == 0);
        slots.GetState(0).Should().Be(SlotState.Waiting);
        slots.FindSlot(B).Should().Be(1);
    }

    [Fact]
    public void Reconnect_WithinGracePeriod_ResumesTheSameSlot()
    {
        var slots = Create(SlotPolicy.Manual);
        slots.OnDeviceAdded(A);
        slots.Assign(A, 2);
        slots.OnDeviceRemoved(A);
        _clock.Advance(TimeSpan.FromSeconds(9));
        slots.Tick().Should().BeEmpty();

        var events = slots.OnDeviceAdded(A);

        events.Should().ContainSingle(e => e.Kind == SlotEventKind.Resumed && e.Slot == 2);
        slots.GetState(2).Should().Be(SlotState.Active);
    }

    [Fact]
    public void GracePeriod_Timeout_EmptiesTheSlot()
    {
        var slots = Create();
        slots.OnDeviceAdded(A);
        slots.OnDeviceRemoved(A);
        _clock.Advance(TimeSpan.FromSeconds(10));

        var events = slots.Tick();

        events.Should().ContainSingle(e => e.Kind == SlotEventKind.Emptied && e.Slot == 0);
        slots.GetState(0).Should().Be(SlotState.Empty);
    }

    [Fact]
    public void Reconnect_AfterTimeout_IsANewDevice()
    {
        var slots = Create();
        slots.OnDeviceAdded(A);
        slots.OnDeviceAdded(B);
        slots.OnDeviceRemoved(A);
        _clock.Advance(TimeSpan.FromSeconds(11));
        slots.Tick();

        slots.OnDeviceAdded(A);

        slots.FindSlot(A).Should().Be(0);
    }

    [Fact]
    public void ZeroGracePeriod_EmptiesImmediately()
    {
        var slots = new SlotManager(_clock, SlotPolicy.Auto, TimeSpan.Zero);
        slots.OnDeviceAdded(A);

        var events = slots.OnDeviceRemoved(A);

        events.Should().ContainSingle(e => e.Kind == SlotEventKind.Emptied);
    }

    [Fact]
    public void ReserveFirstSlot_SkipsSlotZeroForControllers()
    {
        var slots = Create();
        slots.ReserveFirstSlot = true;

        slots.OnDeviceAdded(A);
        slots.OnDeviceAdded(B);

        slots.FindSlot(A).Should().Be(1);
        slots.FindSlot(B).Should().Be(2);
    }

    [Fact]
    public void ReserveFirstSlot_StillAllowsAnExplicitAssign()
    {
        var keyboard = new DeviceId("keyboard");
        var slots = Create();
        slots.ReserveFirstSlot = true;
        slots.OnDeviceAdded(keyboard);

        slots.Assign(keyboard, 0);

        slots.FindSlot(keyboard).Should().Be(0);
    }

    [Fact]
    public void ReserveFirstSlot_IgnoresARememberedSlotZero()
    {
        var slots = Create(SlotPolicy.Remember, new() { ["A"] = 0 });
        slots.ReserveFirstSlot = true;

        slots.OnDeviceAdded(A);

        slots.FindSlot(A).Should().Be(1);
    }

    [Fact]
    public void Reset_EmptiesEverySlotButKeepsDevicesKnown()
    {
        var slots = Create();
        slots.OnDeviceAdded(A);
        slots.OnDeviceAdded(B);

        var events = slots.Reset();
        var again = slots.AutoAssignPresent();

        events.Should().HaveCount(2).And.OnlyContain(e => e.Kind == SlotEventKind.Emptied);
        again.Should().HaveCount(2).And.OnlyContain(e => e.Kind == SlotEventKind.Assigned);
    }

    [Fact]
    public void Paused_ShowsActiveSlotsAsPaused()
    {
        var slots = Create();
        slots.OnDeviceAdded(A);

        slots.Paused = true;

        slots.GetState(0).Should().Be(SlotState.Paused);
        slots.GetState(1).Should().Be(SlotState.Empty);
    }
}
