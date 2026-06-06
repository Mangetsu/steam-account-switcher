using System.Runtime.InteropServices;

namespace EnigmaLauncher.Display;

/// <summary>
/// Enumerates connected monitors and can change the Windows primary display at runtime.
/// Uses the Win32 <c>EnumDisplayDevices</c> / <c>EnumDisplaySettings</c> /
/// <c>ChangeDisplaySettingsEx</c> APIs — no WinForms dependency.
/// </summary>
public static class DisplayManager
{
    // ── Win32 interop ──────────────────────────────────────────────────────────

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice,
        uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(
        string? lpszDeviceName,
        int iModeNum,
        ref DEVMODE lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsEx(
        string? lpszDeviceName,
        ref DEVMODE lpDevMode,
        IntPtr hwnd,
        uint dwflags,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy,
        uint uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public uint   cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint   StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint   dmFields;
        public int    dmPositionX;
        public int    dmPositionY;
        public uint   dmDisplayOrientation;
        public uint   dmDisplayFixedOutput;
        public short  dmColor;
        public short  dmDuplex;
        public short  dmYResolution;
        public short  dmTTOption;
        public short  dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint   dmBitsPerPel;
        public uint   dmPelsWidth;
        public uint   dmPelsHeight;
        public uint   dmDisplayFlags;
        public uint   dmDisplayFrequency;
        public uint   dmICMMethod;
        public uint   dmICMIntent;
        public uint   dmMediaType;
        public uint   dmDitherType;
        public uint   dmReserved1;
        public uint   dmReserved2;
        public uint   dmPanningWidth;
        public uint   dmPanningHeight;
    }

    // StateFlags for DISPLAY_DEVICE
    private const uint DISPLAY_DEVICE_ACTIVE         = 0x00000001;
    private const uint DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;

    // ChangeDisplaySettingsEx flags
    private const uint CDS_UPDATEREGISTRY = 0x00000001;
    private const uint CDS_NORESET        = 0x10000000;

    // DEVMODE field flags
    private const uint DM_POSITION = 0x00000020;

    // SetWindowPos flags
    private const uint SWP_NOSIZE   = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;

    private const int ENUM_CURRENT_SETTINGS   = -1;
    private const int DISP_CHANGE_SUCCESSFUL  = 0;

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all currently active (connected and enabled) monitors.
    /// Safe to call on any thread.
    /// </summary>
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var result = new List<MonitorInfo>();
        int labelIndex = 1;

        var device = new DISPLAY_DEVICE { cb = (uint)Marshal.SizeOf<DISPLAY_DEVICE>() };

        for (uint i = 0; EnumDisplayDevices(null, i, ref device, 0); i++)
        {
            if ((device.StateFlags & DISPLAY_DEVICE_ACTIVE) == 0)
                continue;

            var isPrimary = (device.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;

            // Read current resolution from DEVMODE
            var dm = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
            int w = 0, h = 0;
            if (EnumDisplaySettings(device.DeviceName, ENUM_CURRENT_SETTINGS, ref dm))
            {
                w = (int)dm.dmPelsWidth;
                h = (int)dm.dmPelsHeight;
            }

            var label = isPrimary
                ? $"Display {labelIndex} — {w}×{h} (primary)"
                : $"Display {labelIndex} — {w}×{h}";

            result.Add(new MonitorInfo
            {
                DeviceName   = device.DeviceName,
                DisplayLabel = label,
                IsPrimary    = isPrimary,
                Width        = w,
                Height       = h,
            });

            labelIndex++;
        }

        // Always list primary first
        result.Sort((a, b) => b.IsPrimary.CompareTo(a.IsPrimary));
        return result;
    }

    /// <summary>
    /// Makes <paramref name="targetDevice"/> the Windows primary monitor.
    /// All other monitors are repositioned so they remain non-primary.
    /// </summary>
    /// <param name="targetDevice">GDI device name, e.g. <c>"\\.\DISPLAY2"</c>.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the target device is not found or the API call fails.
    /// </exception>
    public static void SetPrimary(string targetDevice)
    {
        var monitors = GetMonitors();

        var target = monitors.FirstOrDefault(m =>
            string.Equals(m.DeviceName, targetDevice, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Monitor '{targetDevice}' not found. It may have been disconnected.");

        if (target.IsPrimary)
            return; // already primary — nothing to do

        // The Win32 trick: translate every display so the target lands at (0,0), then commit.
        int offsetX = 0, offsetY = 0;

        var device = new DISPLAY_DEVICE { cb = (uint)Marshal.SizeOf<DISPLAY_DEVICE>() };
        for (uint i = 0; EnumDisplayDevices(null, i, ref device, 0); i++)
        {
            if ((device.StateFlags & DISPLAY_DEVICE_ACTIVE) == 0) continue;
            if (!string.Equals(device.DeviceName, targetDevice,
                    StringComparison.OrdinalIgnoreCase)) continue;

            var dm = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
            if (EnumDisplaySettings(device.DeviceName, ENUM_CURRENT_SETTINGS, ref dm))
            {
                offsetX = dm.dmPositionX;
                offsetY = dm.dmPositionY;
            }
            break;
        }

        // Update each active monitor's position
        for (uint i = 0; EnumDisplayDevices(null, i, ref device, 0); i++)
        {
            if ((device.StateFlags & DISPLAY_DEVICE_ACTIVE) == 0) continue;

            var dm = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
            if (!EnumDisplaySettings(device.DeviceName, ENUM_CURRENT_SETTINGS, ref dm))
                continue;

            dm.dmPositionX -= offsetX;
            dm.dmPositionY -= offsetY;
            dm.dmFields     = DM_POSITION;

            var ret = ChangeDisplaySettingsEx(
                device.DeviceName, ref dm, IntPtr.Zero,
                CDS_UPDATEREGISTRY | CDS_NORESET, IntPtr.Zero);

            if (ret != DISP_CHANGE_SUCCESSFUL)
                throw new InvalidOperationException(
                    $"ChangeDisplaySettingsEx failed for '{device.DeviceName}' (return {ret}).");
        }

        // Commit all pending changes
        var empty = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
        ChangeDisplaySettingsEx(null, ref empty, IntPtr.Zero, 0, IntPtr.Zero);
    }

    /// <summary>
    /// Moves the current foreground window to the top-left of <paramref name="targetDevice"/>.
    /// Call this from a background task after a delay so the game window has time to appear.
    /// </summary>
    /// <param name="targetDevice">GDI device name, e.g. <c>"\\.\DISPLAY2"</c>.</param>
    public static void MoveWindowToMonitor(string targetDevice)
    {
        var dm = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
        if (!EnumDisplaySettings(targetDevice, ENUM_CURRENT_SETTINGS, ref dm))
            throw new InvalidOperationException(
                $"Cannot read display settings for '{targetDevice}'.");

        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return; // nothing focused

        // Move window to target monitor's top-left; keep its current size
        SetWindowPos(hwnd, IntPtr.Zero,
            dm.dmPositionX, dm.dmPositionY, 0, 0,
            SWP_NOSIZE | SWP_NOZORDER);
    }
}
