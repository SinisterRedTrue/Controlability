namespace Controlability.Core.Settings;

public enum KeyboardMode
{
    /// <summary>Keyboard and mouse go to the game untouched.</summary>
    KeyboardFirst,

    /// <summary>Keyboard and mouse act as a controller.</summary>
    KeyboardAsController,
}

/// <summary>What the keyboard pad of Mode B sends to the game.</summary>
public enum KeyboardInputStyle
{
    /// <summary>Keys and mouse drive the virtual Xbox pad; the keys are kept from the game.</summary>
    XboxController,

    /// <summary>
    /// The keyboard keeps its player slot (the game still counts that pad as connected) but the pad stays neutral and
    /// nothing is swallowed, so the game reads the real keyboard and mouse with its own default keys.
    /// </summary>
    NormalKeyboard,
}

public enum SlotPolicy
{
    Auto,
    Remember,
    Manual,
}

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public enum AppLogLevel
{
    Debug,
    Information,
    Warning,
    Error,
}

/// <summary>Last main window position and size, in device-independent pixels.</summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized = false);

public sealed record Settings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public bool StartWithWindows { get; set; }

    public bool StartMinimized { get; set; }

    /// <summary>Closing the main window hides it to the tray instead of exiting.</summary>
    public bool CloseToTray { get; set; } = true;

    public KeyboardMode Mode { get; set; } = KeyboardMode.KeyboardFirst;

    /// <summary>Mode B: whether the keyboard acts as an Xbox pad or stays a normal keyboard for the game.</summary>
    public KeyboardInputStyle KeyboardStyle { get; set; } = KeyboardInputStyle.XboxController;

    /// <summary>Mode B: id of the one keyboard that drives the keyboard pad (see the CLI <c>keys</c> command), or "any".</summary>
    public string KeyboardDevice { get; set; } = "any";

    /// <summary>
    /// Mode B: processes (with or without ".exe") whose window must have the focus for keys to be swallowed and the
    /// keyboard pad to listen. Empty: nothing is ever swallowed, so the PC stays usable.
    /// </summary>
    public List<string> KeyboardTargetProcesses { get; set; } = [];

    public SlotPolicy SlotPolicy { get; set; } = SlotPolicy.Auto;

    /// <summary>Engine polling rate; 1000, 500 or 250.</summary>
    public int PollingRateHz { get; set; } = 1000;

    /// <summary>How long a slot keeps its virtual pad after its device vanished (Bluetooth drops).</summary>
    public int GracePeriodSeconds { get; set; } = 10;

    /// <summary>Stable device id to the slot (0..3) it had last; used by <see cref="SlotPolicy.Remember"/>.</summary>
    public Dictionary<string, int> RememberedSlots { get; set; } = [];

    /// <summary>Hide the physical controllers (HidHide) while their virtual pad is active.</summary>
    public bool HidHideEnabled { get; set; } = true;

    /// <summary>Show the physical controllers again while the engine is paused.</summary>
    public bool UnhideWhilePaused { get; set; } = true;

    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>UI language tag such as "en", or "system" to follow Windows.</summary>
    public string Language { get; set; } = "system";

    public AppLogLevel LogLevel { get; set; } = AppLogLevel.Information;

    public WindowPlacement? WindowPlacement { get; set; }
}
