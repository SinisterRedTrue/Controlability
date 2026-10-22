using Controlability.Core.Mapping;
using Controlability.Core.Models;

namespace Controlability.Core.Profiles;

/// <summary>Which devices and games a profile is picked for. Both lists empty = the generic default.</summary>
public sealed record ProfileTargets
{
    public List<ControllerType> ControllerTypes { get; set; } = [];

    /// <summary>Process names, with or without ".exe", compared case-insensitively.</summary>
    public List<string> ProcessNames { get; set; } = [];
}

public sealed record ProfileStick
{
    public DeadZoneMode Mode { get; set; } = DeadZoneMode.Radial;

    public float DeadZone { get; set; } = 0.08f;

    public float Outer { get; set; } = 0.98f;

    public CurveType Curve { get; set; } = CurveType.Linear;

    /// <summary>Control points, only used when <see cref="Curve"/> is <see cref="CurveType.Custom"/>.</summary>
    public List<CurvePoint> Points { get; set; } = [];
}

public sealed record ProfileSticks
{
    public ProfileStick Left { get; set; } = new();

    public ProfileStick Right { get; set; } = new();
}

public sealed record ProfileTriggers
{
    public float DeadZone { get; set; } = 0.02f;

    public bool Digital { get; set; }

    public float DigitalThreshold { get; set; } = 0.5f;
}

public sealed record ProfileExtras
{
    public bool GyroToRightStick { get; set; }

    public bool TouchpadMouse { get; set; }

    /// <summary>Xbox button name to presses per second.</summary>
    public Dictionary<string, int> Turbo { get; set; } = [];
}

/// <summary>Lightbar color as "#RRGGBB".</summary>
public sealed record ProfileLed
{
    public string Color { get; set; } = "#0000FF";
}

/// <summary>Mode B bindings.</summary>
public sealed record ProfileKeyboard
{
    /// <summary>Key or mouse button name (for example "W", "Space", "MouseLeft") to Xbox control (for example "A", "LeftStickUp", "RightTrigger").</summary>
    public Dictionary<string, string> Keys { get; set; } = [];

    public float MouseSensitivity { get; set; } = 1f;

    /// <summary>Time the mouse-driven right stick takes to fall back to center.</summary>
    public int DecayMs { get; set; } = 60;
}

public sealed record Profile
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>In-memory fallback for when no generic profile is stored.</summary>
    public static readonly Profile Generic = new() { Id = "default-generic", Name = "Generic default" };

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public ProfileTargets AppliesTo { get; set; } = new();

    public ProfileSticks Sticks { get; set; } = new();

    public ProfileTriggers Triggers { get; set; } = new();

    /// <summary>
    /// Overrides on top of the generic layout: <see cref="ButtonId"/> name to <see cref="XboxButton"/> name
    /// ("None" disables the button).
    /// </summary>
    public Dictionary<string, string> Buttons { get; set; } = [];

    public ProfileExtras Extras { get; set; } = new();

    public ProfileLed? Led { get; set; }

    public ProfileKeyboard Keyboard { get; set; } = new();
}
