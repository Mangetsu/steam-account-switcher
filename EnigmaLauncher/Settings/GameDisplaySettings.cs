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

    /// <summary>
    /// Seconds to wait after launch before reverting primary back, for
    /// <see cref="DisplaySwitchMethod.SetPrimaryThenRevert"/>. Ignored by other methods.
    /// </summary>
    public int RevertDelaySeconds { get; set; } = 8;
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

    /// <summary>
    /// Like <see cref="SetPrimary"/>, but hands the primary display back to whatever
    /// monitor it was before shortly after launch — so the taskbar/notifications
    /// return to the original screen while the game keeps running on the target one.
    /// Risky for exclusive-fullscreen games: if the revert happens before the game
    /// creates its fullscreen swapchain, the game can end up rendering on the wrong
    /// monitor, get kicked out of exclusive mode, or briefly flicker/black-screen.
    /// </summary>
    SetPrimaryThenRevert,
}
