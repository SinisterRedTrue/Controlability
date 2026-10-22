using Controlability.Core.Abstractions;
using Controlability.Core.Models;
using Controlability.Platform;

namespace Controlability.Engine.Tests.Hardening;

public sealed class CleanupRunnerTests
{
    private sealed class FakeHider(bool throws = false) : IDeviceHider
    {
        public int UnhideAllCalls { get; private set; }

        public bool IsAvailable => true;

        public void Hide(DeviceInfo device)
        {
        }

        public void Unhide(DeviceInfo device)
        {
        }

        public void UnhideAll()
        {
            UnhideAllCalls++;
            if (throws)
            {
                throw new InvalidOperationException("boom");
            }
        }

        public void WhitelistSelf()
        {
        }
    }

    private sealed class FakeAutostart(bool setResult = true) : IAutostart
    {
        public bool? LastSet { get; private set; }

        public bool IsEnabled => LastSet == true;

        public bool Set(bool enabled)
        {
            LastSet = enabled;
            return setResult;
        }
    }

    [Fact]
    public void Unhides_everything_and_removes_autostart()
    {
        var hider = new FakeHider();
        var autostart = new FakeAutostart();

        CleanupRunner.Run(hider, autostart).Should().Be(0);

        hider.UnhideAllCalls.Should().Be(1);
        autostart.LastSet.Should().BeFalse();
    }

    [Fact]
    public void A_failing_unhide_still_removes_autostart_and_reports_failure()
    {
        var autostart = new FakeAutostart();
        var messages = new List<string>();

        CleanupRunner.Run(new FakeHider(throws: true), autostart, messages.Add).Should().Be(1);

        autostart.LastSet.Should().BeFalse();
        messages.Should().ContainSingle(m => m.Contains("unhide failed"));
    }

    [Fact]
    public void A_refused_registry_write_reports_failure()
    {
        CleanupRunner.Run(new FakeHider(), new FakeAutostart(setResult: false)).Should().Be(1);
    }
}
