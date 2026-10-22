using Controlability.Input.Keyboard;

namespace Controlability.Engine.Tests.Input;

public class RawDeviceNameTests
{
    [Theory]
    [InlineData(@"\\?\HID#VID_054C&PID_09CC&MI_03#7&1b2c#{378de44c-56ef-11d1-bc8c-00a0c91405dd}")] // DS4 touchpad
    [InlineData(@"\\?\HID#VID_057E&PID_2009#8&1#{378de44c-56ef-11d1-bc8c-00a0c91405dd}")] // Switch Pro
    public void Controller_touchpads_are_not_mice(string name)
    {
        RawDeviceName.IsControllerPointer(name).Should().BeTrue();
    }

    [Theory]
    [InlineData(@"\\?\HID#VID_046D&PID_C077#7&1#{378de44c-56ef-11d1-bc8c-00a0c91405dd}")]
    [InlineData(@"\\?\ACPI#PNP0F13#4&1#{378de44c-56ef-11d1-bc8c-00a0c91405dd}")]
    public void Real_mice_are_kept(string name)
    {
        RawDeviceName.IsControllerPointer(name).Should().BeFalse();
    }
}
