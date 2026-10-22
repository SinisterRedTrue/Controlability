using Controlability.Platform.HidHide;

namespace Controlability.Engine.Tests.HidHide;

public class HidHideCliParsingTests
{
    // Output of HidHideCLI 1.5.230.0 --dev-gaming on a machine with one DS4 on USB.
    private const string GamingJson = """
        [ { "friendlyName" : "Sony Interactive Entertainment Wireless Controller" , "devices" : [
        { "present" : true ,
        "gamingDevice" : true ,
        "symbolicLink" : "\\\\?\\hid#vid_054c&pid_09cc&mi_03#9&343547ec&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}" ,
        "vendor" : "Sony Interactive Entertainment" ,
        "product" : "Wireless Controller" ,
        "serialNumber" : "" ,
        "usage" : "Gamepad" ,
        "description" : "HID-compliant game controller" ,
        "deviceInstancePath" : "HID\\VID_054C&PID_09CC&MI_03\\9&343547ec&0&0000" ,
        "baseContainerDeviceInstancePath" : "USB\\VID_054C&PID_09CC\\7&4B11830&0&2" ,
        "baseContainerClassGuid" : "{36FC9E60-C465-11CF-8056-444553540000}" ,
        "baseContainerDeviceCount" : 2 } ] } ]
        """;

    [Fact]
    public void Dev_gaming_output_is_parsed()
    {
        var devices = HidDevice.ParseList(GamingJson);

        var device = devices.Should().ContainSingle().Subject;
        device.InstancePath.Should().Be(@"HID\VID_054C&PID_09CC&MI_03\9&343547ec&0&0000");
        device.SymbolicLink.Should().Be(@"\\?\hid#vid_054c&pid_09cc&mi_03#9&343547ec&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}");
        device.ContainerInstancePath.Should().Be(@"USB\VID_054C&PID_09CC\7&4B11830&0&2");
        device.Serial.Should().BeNull();
        device.Present.Should().BeTrue();
        device.Gaming.Should().BeTrue();
        device.TryGetVidPid(out var vid, out var pid).Should().BeTrue();
        vid.Should().Be(0x054C);
        pid.Should().Be(0x09CC);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("[]")]
    public void Empty_output_means_no_devices(string json)
        => HidDevice.ParseList(json).Should().BeEmpty();

    [Fact]
    public void Garbage_output_throws_a_json_exception()
    {
        var act = () => HidDevice.ParseList("not json");

        act.Should().Throw<System.Text.Json.JsonException>();
    }

    [Fact]
    public void List_commands_are_parsed_from_their_quoted_part()
    {
        var output = "--dev-hide \"HID\\VID_054C&PID_09CC&MI_03\\9&343547ec&0&0000\"\r\n\r\n--dev-hide \"HID\\VID_1\\2\"\r\n";

        HidHideCli.ParseQuotedValues(output).Should().Equal(@"HID\VID_054C&PID_09CC&MI_03\9&343547ec&0&0000", @"HID\VID_1\2");
    }

    [Fact]
    public void Lines_without_quotes_are_ignored()
        => HidHideCli.ParseQuotedValues("--inv-off\r\n").Should().BeEmpty();

    [Fact]
    public void Path_lookup_finds_the_first_folder_that_has_the_exe()
    {
        var found = HidHideCli.FindOnPath(@"C:\a;C:\b;C:\c", path => path is @"C:\b\HidHideCLI.exe" or @"C:\c\HidHideCLI.exe");

        found.Should().Be(@"C:\b\HidHideCLI.exe");
    }

    [Fact]
    public void Path_lookup_returns_null_when_nothing_matches()
        => HidHideCli.FindOnPath(@"C:\a;;C:\b", _ => false).Should().BeNull();
}
