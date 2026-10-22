using Controlability.Core.Models;
using Controlability.Platform.HidHide;

namespace Controlability.Engine.Tests.HidHide;

public class DevicePathConverterTests
{
    // Taken from a real DS4 v2 over USB: SDL's path and the matching --dev-gaming entry.
    private const string UsbHidPath = @"\\?\HID#VID_054C&PID_09CC&MI_03#9&343547ec&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string UsbInstancePath = @"HID\VID_054C&PID_09CC&MI_03\9&343547ec&0&0000";

    private const string BluetoothHidPath = @"\\?\HID#{00001124-0000-1000-8000-00805f9b34fb}_VID&0002054c_PID&09cc#8&2b0e2f4a&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string BluetoothInstancePath = @"HID\{00001124-0000-1000-8000-00805f9b34fb}_VID&0002054c_PID&09cc\8&2b0e2f4a&0&0000";

    private static DeviceInfo Device(string? hidPath, ushort vid = 0x054C, ushort pid = 0x09CC, string? serial = "dc-0c-2d-2f-97-e0")
        => new(new DeviceId(serial ?? "x"), "PS4 Controller", vid, pid, serial, hidPath, ConnectionType.Usb, ControllerType.PS4, DeviceCapabilities.None);

    private static HidDevice Entry(string instancePath, string symbolicLink = "", string? container = null, string? serial = null, bool present = true)
        => new(symbolicLink, instancePath, container, serial, present, Gaming: true);

    [Fact]
    public void Usb_path_becomes_the_instance_path()
        => DevicePathConverter.ToInstancePath(UsbHidPath).Should().Be(UsbInstancePath);

    [Fact]
    public void Bluetooth_path_becomes_the_instance_path()
        => DevicePathConverter.ToInstancePath(BluetoothHidPath).Should().Be(BluetoothInstancePath);

    [Fact]
    public void Path_without_the_device_prefix_still_converts()
        => DevicePathConverter.ToInstancePath(UsbHidPath[4..]).Should().Be(UsbInstancePath);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"HID\VID_054C&PID_09CC\1")]
    [InlineData(@"\\?\HID#only-one-part#{4d1e55b2-f16f-11cf-88cb-001111000030}")]
    [InlineData(@"\\?\HID##9&343547ec&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}")]
    public void Anything_else_is_rejected(string? path)
        => DevicePathConverter.ToInstancePath(path).Should().BeNull();

    [Fact]
    public void Resolve_prefers_the_exact_interface_path_ignoring_case()
    {
        var wanted = Entry(UsbInstancePath, UsbHidPath.ToLowerInvariant());
        var other = Entry(@"HID\VID_054C&PID_09CC&MI_00\1&0&0000", @"\\?\hid#vid_054c&pid_09cc&mi_00#1&0&0000#{x}");

        DevicePathConverter.Resolve(Device(UsbHidPath), [other, wanted]).Should().BeSameAs(wanted);
    }

    [Fact]
    public void Resolve_falls_back_to_the_converted_instance_path()
    {
        var wanted = Entry(UsbInstancePath, "something else");

        DevicePathConverter.Resolve(Device(UsbHidPath), [wanted]).Should().BeSameAs(wanted);
    }

    [Fact]
    public void Resolve_falls_back_to_vid_pid_when_the_path_is_unknown()
    {
        var wanted = Entry(UsbInstancePath);
        var otherModel = Entry(@"HID\VID_046D&PID_C21D\1");

        DevicePathConverter.Resolve(Device(null), [otherModel, wanted]).Should().BeSameAs(wanted);
    }

    [Fact]
    public void Resolve_matches_a_bluetooth_vid_pid()
    {
        var wanted = Entry(BluetoothInstancePath);

        DevicePathConverter.Resolve(Device(null), [wanted]).Should().BeSameAs(wanted);
    }

    [Fact]
    public void Resolve_ignores_devices_that_are_not_present()
        => DevicePathConverter.Resolve(Device(null), [Entry(UsbInstancePath, present: false)]).Should().BeNull();

    [Fact]
    public void Resolve_uses_the_serial_to_tell_two_identical_pads_apart()
    {
        var first = Entry(@"HID\VID_054C&PID_09CC&MI_03\a", container: @"USB\VID_054C&PID_09CC\AAAA1111");
        var second = Entry(@"HID\VID_054C&PID_09CC&MI_03\b", serial: "dc-0c-2d-2f-97-e0");

        DevicePathConverter.Resolve(Device(null), [first, second]).Should().BeSameAs(second);
    }

    [Fact]
    public void Resolve_gives_up_when_two_identical_pads_cannot_be_told_apart()
    {
        var first = Entry(@"HID\VID_054C&PID_09CC&MI_03\a");
        var second = Entry(@"HID\VID_054C&PID_09CC&MI_03\b");

        DevicePathConverter.Resolve(Device(null, serial: null), [first, second]).Should().BeNull();
    }
}
