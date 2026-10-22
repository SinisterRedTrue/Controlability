namespace Controlability.Core.Settings;

public enum KeyboardMode
{
    /// <summary>Keyboard and mouse go to the game untouched.</summary>
    KeyboardFirst,

    /// <summary>Keyboard and mouse act as a controller.</summary>
    KeyboardAsController,
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

    public KeyboardMode Mode { get; set; } = KeyboardMode.KeyboardFirst;

    public SlotPolicy SlotPolicy { get; set; } = SlotPolicy.Auto;

    /// <summary>Engine polling rate; 1000, 500 or 250.</summary>
    public int PollingRateHz { get; set; } = 1000;

    /// <summary>How long a slot keeps its virtual pad after its device vanished (Bluetooth drops).</summary>
    public int GracePeriodSeconds { get; set; } = 10;

    /// <summary>Stable device id to the slot (0..3) it had last; used by <see cref="SlotPolicy.Remember"/>.</summary>
    public Dictionary<string, int> RememberedSlots { get; set; } = [];

    public bool HidHideEnabled { get; set; } = true;

    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>UI language tag such as "en", or "system" to follow Windows.</summary>
    public string Language { get; set; } = "system";

    public AppLogLevel LogLevel { get; set; } = AppLogLevel.Information;

    public WindowPlacement? WindowPlacement { get; set; }
}
