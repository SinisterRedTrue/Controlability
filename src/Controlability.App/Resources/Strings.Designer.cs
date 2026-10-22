using System.Globalization;
using System.Resources;

namespace Controlability.App.Resources;

/// <summary>Typed access to <c>Strings.resx</c>. Keep in step with the file (one property per key).</summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new("Controlability.App.Resources.Strings", typeof(Strings).Assembly);

    private static string Get(string name) => Manager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

    public static string AppName => Get(nameof(AppName));

    public static string NavDashboard => Get(nameof(NavDashboard));

    public static string NavDevices => Get(nameof(NavDevices));

    public static string NavProfiles => Get(nameof(NavProfiles));

    public static string NavSettings => Get(nameof(NavSettings));

    public static string ComingSoonTitle => Get(nameof(ComingSoonTitle));

    public static string ComingSoonDevices => Get(nameof(ComingSoonDevices));

    public static string ComingSoonProfiles => Get(nameof(ComingSoonProfiles));

    public static string ComingSoonSettings => Get(nameof(ComingSoonSettings));

    public static string DashboardTitle => Get(nameof(DashboardTitle));

    public static string PlayerTitle => Get(nameof(PlayerTitle));

    public static string SlotEmptyStatus => Get(nameof(SlotEmptyStatus));

    public static string SlotActiveStatus => Get(nameof(SlotActiveStatus));

    public static string SlotWaitingStatus => Get(nameof(SlotWaitingStatus));

    public static string SlotPausedStatus => Get(nameof(SlotPausedStatus));

    public static string SlotEmptyHint => Get(nameof(SlotEmptyHint));

    public static string ProfileLabel => Get(nameof(ProfileLabel));

    public static string ProfileNone => Get(nameof(ProfileNone));

    public static string XInputLabel => Get(nameof(XInputLabel));

    public static string XInputUnknown => Get(nameof(XInputUnknown));

    public static string ConnectionUsb => Get(nameof(ConnectionUsb));

    public static string ConnectionBluetooth => Get(nameof(ConnectionBluetooth));

    public static string ConnectionUnknown => Get(nameof(ConnectionUnknown));

    public static string BatteryWired => Get(nameof(BatteryWired));

    public static string BatteryPercent => Get(nameof(BatteryPercent));

    public static string BatteryCharging => Get(nameof(BatteryCharging));

    public static string BatteryUnknown => Get(nameof(BatteryUnknown));

    public static string TypeGeneric => Get(nameof(TypeGeneric));

    public static string TypePS4 => Get(nameof(TypePS4));

    public static string TypePS5 => Get(nameof(TypePS5));

    public static string TypeSwitchPro => Get(nameof(TypeSwitchPro));

    public static string TypeXbox => Get(nameof(TypeXbox));

    public static string TypeKeyboard => Get(nameof(TypeKeyboard));

    public static string DetectedDevices => Get(nameof(DetectedDevices));

    public static string DetectedDevicesHint => Get(nameof(DetectedDevicesHint));

    public static string DevicesEmptyTitle => Get(nameof(DevicesEmptyTitle));

    public static string DevicesEmptyHint => Get(nameof(DevicesEmptyHint));

    public static string DeviceAssigned => Get(nameof(DeviceAssigned));

    public static string DeviceUnassigned => Get(nameof(DeviceUnassigned));

    public static string AssignToPlayer => Get(nameof(AssignToPlayer));

    public static string MenuUnassign => Get(nameof(MenuUnassign));

    public static string MenuChangeProfile => Get(nameof(MenuChangeProfile));

    public static string MenuIdentify => Get(nameof(MenuIdentify));

    public static string MenuNoProfiles => Get(nameof(MenuNoProfiles));

    public static string ModeLabel => Get(nameof(ModeLabel));

    public static string ModeAShort => Get(nameof(ModeAShort));

    public static string ModeBShort => Get(nameof(ModeBShort));

    public static string ModeATitle => Get(nameof(ModeATitle));

    public static string ModeBTitle => Get(nameof(ModeBTitle));

    public static string ModeATooltip => Get(nameof(ModeATooltip));

    public static string ModeBTooltip => Get(nameof(ModeBTooltip));

    public static string PauseEmulation => Get(nameof(PauseEmulation));

    public static string PauseTooltip => Get(nameof(PauseTooltip));

    public static string DriverInstalled => Get(nameof(DriverInstalled));

    public static string DriverMissing => Get(nameof(DriverMissing));

    public static string BannerViGEmTitle => Get(nameof(BannerViGEmTitle));

    public static string BannerViGEmText => Get(nameof(BannerViGEmText));

    public static string BannerHidHideTitle => Get(nameof(BannerHidHideTitle));

    public static string BannerHidHideText => Get(nameof(BannerHidHideText));

    public static string BannerInstall => Get(nameof(BannerInstall));

    public static string BannerInstallHint => Get(nameof(BannerInstallHint));

    public static string TrayOpen => Get(nameof(TrayOpen));

    public static string TrayPause => Get(nameof(TrayPause));

    public static string TrayMode => Get(nameof(TrayMode));

    public static string TrayModeA => Get(nameof(TrayModeA));

    public static string TrayModeB => Get(nameof(TrayModeB));

    public static string TraySlotEmpty => Get(nameof(TraySlotEmpty));

    public static string TraySlotFilled => Get(nameof(TraySlotFilled));

    public static string TrayExit => Get(nameof(TrayExit));

    public static string TrayTooltipNormal => Get(nameof(TrayTooltipNormal));

    public static string TrayTooltipPaused => Get(nameof(TrayTooltipPaused));

    public static string TrayTooltipError => Get(nameof(TrayTooltipError));

    public static string TrayStillRunningTitle => Get(nameof(TrayStillRunningTitle));

    public static string TrayStillRunningText => Get(nameof(TrayStillRunningText));

    public static string NotifyConnected => Get(nameof(NotifyConnected));

    public static string NotifyConnectedUnassigned => Get(nameof(NotifyConnectedUnassigned));

    public static string NotifyDisconnected => Get(nameof(NotifyDisconnected));

    public static string NotifyTitle => Get(nameof(NotifyTitle));

    public static string KeyboardStyleLabel => Get(nameof(KeyboardStyleLabel));

    public static string StyleXboxShort => Get(nameof(StyleXboxShort));

    public static string StyleKeyboardShort => Get(nameof(StyleKeyboardShort));

    public static string StyleXboxTooltip => Get(nameof(StyleXboxTooltip));

    public static string StyleKeyboardTooltip => Get(nameof(StyleKeyboardTooltip));

    public static string StyleOnlyModeB => Get(nameof(StyleOnlyModeB));

    public static string SettingsTitle => Get(nameof(SettingsTitle));

    public static string SettingsGeneral => Get(nameof(SettingsGeneral));

    public static string CloseToTray => Get(nameof(CloseToTray));

    public static string CloseToTrayHint => Get(nameof(CloseToTrayHint));

    public static string SettingsGameTitle => Get(nameof(SettingsGameTitle));

    public static string SettingsGameHint => Get(nameof(SettingsGameHint));

    public static string GameNone => Get(nameof(GameNone));

    public static string GameAdd => Get(nameof(GameAdd));

    public static string GameRemove => Get(nameof(GameRemove));

    public static string GameNameHint => Get(nameof(GameNameHint));

    public static string GameRunning => Get(nameof(GameRunning));

    public static string GameRefresh => Get(nameof(GameRefresh));

    public static string GameAddSelected => Get(nameof(GameAddSelected));

    public static string SettingsKeysTitle => Get(nameof(SettingsKeysTitle));

    public static string SettingsKeysHint => Get(nameof(SettingsKeysHint));

    public static string KeyNotBound => Get(nameof(KeyNotBound));

    public static string KeyChange => Get(nameof(KeyChange));

    public static string KeyClear => Get(nameof(KeyClear));

    public static string KeyPressPrompt => Get(nameof(KeyPressPrompt));

    public static string KeyCancel => Get(nameof(KeyCancel));

    public static string KeysReset => Get(nameof(KeysReset));

    public static string KeysNoProfile => Get(nameof(KeysNoProfile));

    public static string KeysProfile => Get(nameof(KeysProfile));

    public static string SettingsPadTitle => Get(nameof(SettingsPadTitle));

    public static string SettingsPadHint => Get(nameof(SettingsPadHint));

    public static string PadNone => Get(nameof(PadNone));

    public static string PadSummary => Get(nameof(PadSummary));

    public static string ColXbox => Get(nameof(ColXbox));

    public static string ColKeyboard => Get(nameof(ColKeyboard));

    public static string ColController => Get(nameof(ColController));

    public static string PadNotMapped => Get(nameof(PadNotMapped));

    public static string Format(string template, params object[] args) => string.Format(CultureInfo.CurrentCulture, template, args);
}
