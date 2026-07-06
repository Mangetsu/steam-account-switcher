namespace EnigmaLauncher.Display;

/// <summary>
/// Represents a physical or virtual monitor available on the system.
/// </summary>
public class MonitorInfo
{
    /// <summary>
    /// GDI device name, e.g. <c>"\\.\DISPLAY1"</c>.
    /// Used as the stable identifier for persisting per-game display preferences.
    /// </summary>
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>Human-readable label shown in the UI (e.g. "Display 1 (primary)").</summary>
    public string DisplayLabel { get; init; } = string.Empty;

    /// <summary>True when this monitor is the current Windows primary display.</summary>
    public bool IsPrimary { get; init; }

    /// <summary>Horizontal resolution in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Vertical resolution in pixels.</summary>
    public int Height { get; init; }

    /// <inheritdoc/>
    public override string ToString() => DisplayLabel;
}
