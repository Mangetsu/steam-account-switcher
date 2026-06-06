namespace EnigmaLauncher.Settings;

/// <summary>Per-game display routing preferences.</summary>
public class GameDisplaySettings
{
    /// <summary>
    /// GDI device name of the target monitor, e.g. <c>"\\.\DISPLAY2"</c>.
    /// Null means "no per-game override — use global default or current primary".
    /// </summary>
    public string? TargetDevice { get; set; }

    /// <summary>
    /// Strategy used to route the game to the chosen monitor.
    /// </summary>
    public DisplaySwitchMethod Method { get; set; } = DisplaySwitchMethod.None;
}

/// <summary>
/// How to move a launched game to the target display.
/// </summary>
public enum DisplaySwitchMethod
{
    /// <summary>No display switching — launch normally.</summary>
    None,

    /// <summary>
    /// Make the target monitor the Windows primary display before launching.
    /// Works for full-screen games that always open on the primary.
    /// </summary>
    SetPrimary,

    /// <summary>
    /// After launch, poll for the game window and use SetWindowPos to move
    /// it to the target monitor.  Works for windowed / borderless games.
    /// </summary>
    MoveWindow,
}
