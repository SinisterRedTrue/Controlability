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

    public static string StatusSaved => Get(nameof(StatusSaved));

    public static string StatusReverted => Get(nameof(StatusReverted));

    public static string StatusReset => Get(nameof(StatusReset));

    public static string BindPrompt => Get(nameof(BindPrompt));

    public static string BindNoController => Get(nameof(BindNoController));

    public static string BindTimedOut => Get(nameof(BindTimedOut));

    public static string ErrNameEmpty => Get(nameof(ErrNameEmpty));

    public static string ErrLeftDeadZone => Get(nameof(ErrLeftDeadZone));

    public static string ErrRightDeadZone => Get(nameof(ErrRightDeadZone));

    public static string BrowseGameTitle => Get(nameof(BrowseGameTitle));

    public static string FileFilterPrograms => Get(nameof(FileFilterPrograms));

    public static string FileFilterProfiles => Get(nameof(FileFilterProfiles));

    public static string DiscardTitle => Get(nameof(DiscardTitle));

    public static string DiscardMessage => Get(nameof(DiscardMessage));

    public static string DeleteTitle => Get(nameof(DeleteTitle));

    public static string DeleteMessage => Get(nameof(DeleteMessage));

    public static string ImportTitle => Get(nameof(ImportTitle));

    public static string ExportTitle => Get(nameof(ExportTitle));

    public static string CopyName => Get(nameof(CopyName));

    public static string StatusDuplicated => Get(nameof(StatusDuplicated));

    public static string StatusDeleted => Get(nameof(StatusDeleted));

    public static string StatusImported => Get(nameof(StatusImported));

    public static string StatusImportFailed => Get(nameof(StatusImportFailed));

    public static string StatusExported => Get(nameof(StatusExported));

    public static string CapRumble => Get(nameof(CapRumble));

    public static string CapLed => Get(nameof(CapLed));

    public static string CapGyro => Get(nameof(CapGyro));

    public static string CapTouchpad => Get(nameof(CapTouchpad));

    public static string CapBattery => Get(nameof(CapBattery));

    public static string CapNone => Get(nameof(CapNone));

    public static string LedInvalid => Get(nameof(LedInvalid));

    public static string DevicesDetected => Get(nameof(DevicesDetected));

    public static string LabelType => Get(nameof(LabelType));

    public static string LabelConnection => Get(nameof(LabelConnection));

    public static string LabelVidPid => Get(nameof(LabelVidPid));

    public static string LabelSerial => Get(nameof(LabelSerial));

    public static string LabelHidPath => Get(nameof(LabelHidPath));

    public static string LabelCapabilities => Get(nameof(LabelCapabilities));

    public static string LabelAssignment => Get(nameof(LabelAssignment));

    public static string HideFromGames => Get(nameof(HideFromGames));

    public static string HideFromGamesHint => Get(nameof(HideFromGamesHint));

    public static string HidHideMissingHint => Get(nameof(HidHideMissingHint));

    public static string RumbleTest => Get(nameof(RumbleTest));

    public static string LedColor => Get(nameof(LedColor));

    public static string LedApply => Get(nameof(LedApply));

    public static string LivePanelRaw => Get(nameof(LivePanelRaw));

    public static string LivePanelOutput => Get(nameof(LivePanelOutput));

    public static string LiveNoState => Get(nameof(LiveNoState));

    public static string ProfilesList => Get(nameof(ProfilesList));

    public static string ProfilesImport => Get(nameof(ProfilesImport));

    public static string ProfilesExport => Get(nameof(ProfilesExport));

    public static string ProfilesDuplicate => Get(nameof(ProfilesDuplicate));

    public static string ProfilesDelete => Get(nameof(ProfilesDelete));

    public static string ProfilesDeleteShipped => Get(nameof(ProfilesDeleteShipped));

    public static string ProfilesEmptyTitle => Get(nameof(ProfilesEmptyTitle));

    public static string ProfilesEmptyHint => Get(nameof(ProfilesEmptyHint));

    public static string EditorSave => Get(nameof(EditorSave));

    public static string EditorRevert => Get(nameof(EditorRevert));

    public static string EditorReset => Get(nameof(EditorReset));

    public static string EditorName => Get(nameof(EditorName));

    public static string TabButtons => Get(nameof(TabButtons));

    public static string TabSticks => Get(nameof(TabSticks));

    public static string TabTriggers => Get(nameof(TabTriggers));

    public static string TabKeyboard => Get(nameof(TabKeyboard));

    public static string TabAppliesTo => Get(nameof(TabAppliesTo));

    public static string ButtonsHint => Get(nameof(ButtonsHint));

    public static string ButtonBind => Get(nameof(ButtonBind));

    public static string ButtonClear => Get(nameof(ButtonClear));

    public static string ButtonCancel => Get(nameof(ButtonCancel));

    public static string StickLeft => Get(nameof(StickLeft));

    public static string StickRight => Get(nameof(StickRight));

    public static string StickInner => Get(nameof(StickInner));

    public static string StickOuter => Get(nameof(StickOuter));

    public static string StickCurve => Get(nameof(StickCurve));

    public static string StickPreviewHint => Get(nameof(StickPreviewHint));

    public static string TriggerDeadZoneLabel => Get(nameof(TriggerDeadZoneLabel));

    public static string TriggerDigitalLabel => Get(nameof(TriggerDigitalLabel));

    public static string TriggerThresholdLabel => Get(nameof(TriggerThresholdLabel));

    public static string KeyboardTabHint => Get(nameof(KeyboardTabHint));

    public static string MouseSensitivity => Get(nameof(MouseSensitivity));

    public static string MouseDecay => Get(nameof(MouseDecay));

    public static string AppliesControllers => Get(nameof(AppliesControllers));

    public static string AppliesGames => Get(nameof(AppliesGames));

    public static string AppliesGamesHint => Get(nameof(AppliesGamesHint));

    public static string AppliesNoGames => Get(nameof(AppliesNoGames));

    public static string AppliesBrowse => Get(nameof(AppliesBrowse));

    public static string AppliesAddRunning => Get(nameof(AppliesAddRunning));

    public static string AppliesRefresh => Get(nameof(AppliesRefresh));

    public static string TabKeyboardOnly => Get(nameof(TabKeyboardOnly));

    public static string TabExtras => Get(nameof(TabExtras));

    public static string ExtrasGyroTitle => Get(nameof(ExtrasGyroTitle));

    public static string ExtrasGyroHint => Get(nameof(ExtrasGyroHint));

    public static string ExtrasGyroEnable => Get(nameof(ExtrasGyroEnable));

    public static string ExtrasGyroSensX => Get(nameof(ExtrasGyroSensX));

    public static string ExtrasGyroSensY => Get(nameof(ExtrasGyroSensY));

    public static string ExtrasGyroInvertX => Get(nameof(ExtrasGyroInvertX));

    public static string ExtrasGyroInvertY => Get(nameof(ExtrasGyroInvertY));

    public static string ExtrasGyroSmoothing => Get(nameof(ExtrasGyroSmoothing));

    public static string ExtrasGyroActivation => Get(nameof(ExtrasGyroActivation));

    public static string ExtrasGyroButton => Get(nameof(ExtrasGyroButton));

    public static string GyroActAlways => Get(nameof(GyroActAlways));

    public static string GyroActHeld => Get(nameof(GyroActHeld));

    public static string GyroActToggle => Get(nameof(GyroActToggle));

    public static string GyroButtonNone => Get(nameof(GyroButtonNone));

    public static string ExtrasTouchpadTitle => Get(nameof(ExtrasTouchpadTitle));

    public static string ExtrasTouchpadHint => Get(nameof(ExtrasTouchpadHint));

    public static string ExtrasTouchpadEnable => Get(nameof(ExtrasTouchpadEnable));

    public static string ExtrasTouchpadSpeed => Get(nameof(ExtrasTouchpadSpeed));

    public static string ExtrasTouchpadAccel => Get(nameof(ExtrasTouchpadAccel));

    public static string ExtrasButtonsTitle => Get(nameof(ExtrasButtonsTitle));

    public static string ExtrasButtonsHint => Get(nameof(ExtrasButtonsHint));

    public static string ExtrasTurbo => Get(nameof(ExtrasTurbo));

    public static string ExtrasToggle => Get(nameof(ExtrasToggle));

    public static string ExtrasHz => Get(nameof(ExtrasHz));

    public static string DashboardActiveGame => Get(nameof(DashboardActiveGame));

    public static string NotifyBatteryLow => Get(nameof(NotifyBatteryLow));

    public static string NotifyBatteryCritical => Get(nameof(NotifyBatteryCritical));

    public static string TrayTooltipBattery => Get(nameof(TrayTooltipBattery));

    public static string SettingsInput => Get(nameof(SettingsInput));

    public static string SettingsHiding => Get(nameof(SettingsHiding));

    public static string SettingsAppearance => Get(nameof(SettingsAppearance));

    public static string SettingsAdvanced => Get(nameof(SettingsAdvanced));

    public static string SettingsAbout => Get(nameof(SettingsAbout));

    public static string StartWithWindows => Get(nameof(StartWithWindows));

    public static string StartWithWindowsHint => Get(nameof(StartWithWindowsHint));

    public static string StartMinimized => Get(nameof(StartMinimized));

    public static string AutostartFailed => Get(nameof(AutostartFailed));

    public static string ModeSettingLabel => Get(nameof(ModeSettingLabel));

    public static string SlotPolicyLabel => Get(nameof(SlotPolicyLabel));

    public static string SlotPolicyHint => Get(nameof(SlotPolicyHint));

    public static string PolicyAuto => Get(nameof(PolicyAuto));

    public static string PolicyRemember => Get(nameof(PolicyRemember));

    public static string PolicyManual => Get(nameof(PolicyManual));

    public static string PollingRateLabel => Get(nameof(PollingRateLabel));

    public static string PollingRateHint => Get(nameof(PollingRateHint));

    public static string GraceLabel => Get(nameof(GraceLabel));

    public static string GraceHint => Get(nameof(GraceHint));

    public static string GraceNone => Get(nameof(GraceNone));

    public static string GraceSeconds => Get(nameof(GraceSeconds));

    public static string HidHideEnabled => Get(nameof(HidHideEnabled));

    public static string HidHideHint => Get(nameof(HidHideHint));

    public static string UnhideWhilePaused => Get(nameof(UnhideWhilePaused));

    public static string UnhideAllHint => Get(nameof(UnhideAllHint));

    public static string UnhideAllButton => Get(nameof(UnhideAllButton));

    public static string UnhideAllTitle => Get(nameof(UnhideAllTitle));

    public static string UnhideAllConfirm => Get(nameof(UnhideAllConfirm));

    public static string UnhideAllDone => Get(nameof(UnhideAllDone));

    public static string ThemeLabel => Get(nameof(ThemeLabel));

    public static string ThemeSystem => Get(nameof(ThemeSystem));

    public static string ThemeLight => Get(nameof(ThemeLight));

    public static string ThemeDark => Get(nameof(ThemeDark));

    public static string LanguageLabel => Get(nameof(LanguageLabel));

    public static string LanguageSystem => Get(nameof(LanguageSystem));

    public static string LanguageRestart => Get(nameof(LanguageRestart));

    public static string LogLevelLabel => Get(nameof(LogLevelLabel));

    public static string LogDebug => Get(nameof(LogDebug));

    public static string LogInformation => Get(nameof(LogInformation));

    public static string LogWarning => Get(nameof(LogWarning));

    public static string LogError => Get(nameof(LogError));

    public static string OpenLogs => Get(nameof(OpenLogs));

    public static string CopyDiagnostics => Get(nameof(CopyDiagnostics));

    public static string DiagnosticsCopied => Get(nameof(DiagnosticsCopied));

    public static string DiagnosticsCopyFailed => Get(nameof(DiagnosticsCopyFailed));

    public static string AboutVersion => Get(nameof(AboutVersion));

    public static string AboutLicenses => Get(nameof(AboutLicenses));

    public static string AboutOpen => Get(nameof(AboutOpen));

    public static string CrashTitle => Get(nameof(CrashTitle));

    public static string CrashHeading => Get(nameof(CrashHeading));

    public static string CrashText => Get(nameof(CrashText));

    public static string CrashCopy => Get(nameof(CrashCopy));

    public static string CrashClose => Get(nameof(CrashClose));

    public static string SteamTipTitle => Get(nameof(SteamTipTitle));

    public static string SteamTipText => Get(nameof(SteamTipText));

    public static string SteamTipDismiss => Get(nameof(SteamTipDismiss));

    public static string WizardWindowTitle => Get(nameof(WizardWindowTitle));

    public static string WizardStepOf => Get(nameof(WizardStepOf));

    public static string WizardWelcomeTitle => Get(nameof(WizardWelcomeTitle));

    public static string WizardWelcomeText => Get(nameof(WizardWelcomeText));

    public static string WizardWelcomeSteps => Get(nameof(WizardWelcomeSteps));

    public static string WizardDriversTitle => Get(nameof(WizardDriversTitle));

    public static string WizardDriversText => Get(nameof(WizardDriversText));

    public static string WizardViGEmWhy => Get(nameof(WizardViGEmWhy));

    public static string WizardHidHideWhy => Get(nameof(WizardHidHideWhy));

    public static string WizardDriverInstalled => Get(nameof(WizardDriverInstalled));

    public static string WizardDriverMissing => Get(nameof(WizardDriverMissing));

    public static string WizardInstall => Get(nameof(WizardInstall));

    public static string WizardInstallViGEm => Get(nameof(WizardInstallViGEm));

    public static string WizardInstallHidHide => Get(nameof(WizardInstallHidHide));

    public static string WizardRebootNotice => Get(nameof(WizardRebootNotice));

    public static string WizardRechecking => Get(nameof(WizardRechecking));

    public static string WizardControllerTitle => Get(nameof(WizardControllerTitle));

    public static string WizardControllerText => Get(nameof(WizardControllerText));

    public static string WizardControllerWaiting => Get(nameof(WizardControllerWaiting));

    public static string WizardControllerFound => Get(nameof(WizardControllerFound));

    public static string WizardModeTitle => Get(nameof(WizardModeTitle));

    public static string WizardModeText => Get(nameof(WizardModeText));

    public static string WizardModeLater => Get(nameof(WizardModeLater));

    public static string WizardDoneTitle => Get(nameof(WizardDoneTitle));

    public static string WizardDoneText => Get(nameof(WizardDoneText));

    public static string WizardBack => Get(nameof(WizardBack));

    public static string WizardNext => Get(nameof(WizardNext));

    public static string WizardFinish => Get(nameof(WizardFinish));

    public static string WizardSkip => Get(nameof(WizardSkip));

    public static string Format(string template, params object[] args) => string.Format(CultureInfo.CurrentCulture, template, args);
}
