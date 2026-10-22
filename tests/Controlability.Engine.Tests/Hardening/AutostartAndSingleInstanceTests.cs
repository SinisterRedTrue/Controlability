using Controlability.Platform;
using Microsoft.Win32;

namespace Controlability.Engine.Tests.Hardening;

public sealed class AutostartTests : IDisposable
{
    private readonly string _keyPath = $@"Software\ControlabilityTests\{Guid.NewGuid():N}";

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(@"Software\ControlabilityTests", throwOnMissingSubKey: false);

    private RegistryAutostart Create(string? exe = @"C:\Apps\Controlability\Controlability.App.exe")
        => new(_keyPath, "ControlabilityTest", () => exe);

    [Fact]
    public void Enabling_writes_a_quoted_path_with_the_minimized_flag()
    {
        var autostart = Create();

        autostart.Set(true).Should().BeTrue();

        autostart.IsEnabled.Should().BeTrue();
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        key!.GetValue("ControlabilityTest").Should().Be("\"C:\\Apps\\Controlability\\Controlability.App.exe\" --minimized");
    }

    [Fact]
    public void Disabling_removes_the_value_and_is_safe_twice()
    {
        var autostart = Create();
        autostart.Set(true);

        autostart.Set(false).Should().BeTrue();
        autostart.Set(false).Should().BeTrue();

        autostart.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Without_a_known_exe_path_enabling_fails_without_throwing()
    {
        var autostart = Create(exe: null);

        autostart.Set(true).Should().BeFalse();
        autostart.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void The_default_run_key_is_the_current_user_one()
        => RegistryAutostart.RunKeyPath.Should().Be(@"Software\Microsoft\Windows\CurrentVersion\Run");
}

public sealed class SingleInstanceTests
{
    private static string UniqueName() => $"Controlability-Test-{Guid.NewGuid():N}";

    [Fact]
    public void The_first_copy_owns_the_mutex_and_a_second_copy_does_not()
    {
        var name = UniqueName();
        using var first = SingleInstance.Create(name);
        using var second = SingleInstance.Create(name);

        first.IsFirst.Should().BeTrue();
        second.IsFirst.Should().BeFalse();
    }

    [Fact]
    public void A_signal_from_the_second_copy_reaches_the_listener_of_the_first()
    {
        var name = UniqueName();
        using var first = SingleInstance.Create(name);
        using var second = SingleInstance.Create(name);
        using var received = new ManualResetEventSlim();
        first.Listen(received.Set);

        second.SignalFirst();

        received.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
    }

    [Fact]
    public void After_the_first_copy_exits_a_new_copy_becomes_first()
    {
        var name = UniqueName();
        var first = SingleInstance.Create(name);
        first.Dispose();

        using var next = SingleInstance.Create(name);

        next.IsFirst.Should().BeTrue();
    }
}
