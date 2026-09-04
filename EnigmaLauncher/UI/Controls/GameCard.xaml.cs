using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using EnigmaLauncher.Display;
using EnigmaLauncher.Settings;
using EnigmaLauncher.Stores;

namespace EnigmaLauncher.UI.Controls;

public partial class GameCard : UserControl
{
    public GameInfo?    Game  { get; private set; }
    public AccountInfo? Owner { get; private set; }

    /// <summary>Per-game display routing preference. Set by the parent window after Initialize().</summary>
    private GameDisplaySettings? _displaySettings;
    public GameDisplaySettings? DisplaySettings
    {
        get => _displaySettings;
        set
        {
            _displaySettings = value;
            UpdateDisplayNumber();
        }
    }

    /// <summary>Raised when the user clicks Play or double-clicks the card.</summary>
    public event EventHandler<GameInfo>? PlayRequested;

    /// <summary>Raised when the user asks to open the game in its store library.</summary>
    public event EventHandler<GameInfo>? OpenInLibraryRequested;

    /// <summary>Raised when the user clicks Create Desktop Shortcut.</summary>
    public event EventHandler<GameInfo>? ShortcutRequested;

    /// <summary>Raised when the user clicks Create a shortcut (choose location).</summary>
    public event EventHandler<GameInfo>? ShortcutLocationRequested;

    /// <summary>Raised when the user saves or clears display settings for this game.</summary>
    public event EventHandler<(GameInfo Game, GameDisplaySettings Settings)>? DisplaySettingsChanged;

    public GameCard()
    {
        InitializeComponent();
        MouseDoubleClick += OnMouseDoubleClick;
    }

    public void Initialize(GameInfo game, AccountInfo? owner)
    {
        Game  = game;
        Owner = owner;

        GameNameText.Text = game.Name;
        HoverNameText.Text = game.Name;
        Badge.Account     = owner;
        Badge.Refresh();

        if (owner is not null && !owner.CanAutoSwitch)
        {
            var message = $"Cannot auto-switch to '{owner.DisplayName}'.\n" +
                          "Log in to that account with 'Remember me' checked.";
            DesktopShortcutButton.IsEnabled = false;
            DesktopShortcutButton.ToolTip   = message;
            CustomShortcutButton.IsEnabled  = false;
            CustomShortcutButton.ToolTip    = message;
            OpenInLibraryButton.IsEnabled   = false;
            OpenInLibraryButton.ToolTip     = message;
        }
    }

    public void SetArtwork(BitmapImage image)
    {
        ArtworkImage.Source     = image;
        ArtworkImage.Visibility = Visibility.Visible;
        Placeholder.Visibility  = Visibility.Collapsed;
    }

    private void UpdateDisplayNumber()
    {
        try
        {
            var monitors = DisplayManager.GetMonitors();
            var target = DisplaySettings?.TargetDevice is string device
                ? monitors.FirstOrDefault(m => string.Equals(m.DeviceName, device, StringComparison.OrdinalIgnoreCase))
                : monitors.FirstOrDefault(m => m.IsPrimary);
            DisplayNumberText.Text = target is null
                ? "—"
                : target.DisplayLabel.Split('—')[0].Replace("Display", "", StringComparison.OrdinalIgnoreCase).Trim();
        }
        catch
        {
            DisplayNumberText.Text = "—";
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (Game is not null)
            PlayRequested?.Invoke(this, Game);
    }

    private void OpenInLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Game is not null)
            OpenInLibraryRequested?.Invoke(this, Game);
    }

    private void DesktopShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Game is not null)
            ShortcutRequested?.Invoke(this, Game);
    }

    private void CustomShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Game is not null)
            ShortcutLocationRequested?.Invoke(this, Game);
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Popup content (display-settings popup) is only connected to the card through the
        // logical tree, not the visual tree, but routed events still bubble through it into
        // this handler — so rapid clicks on the stepper/combo boxes inside the popup were
        // being misread as a double-click on the card and launching the game underneath it.
        if (DisplaySettingsPopup.IsOpen) return;

        if (Game is not null)
            PlayRequested?.Invoke(this, Game);
    }

    // ── Display settings popup ────────────────────────────────────────────────

    private void DisplaySettingsButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        PopulateDisplaySettingsPopup();
        DisplaySettingsPopup.IsOpen = true;
    }

    private void PopulateDisplaySettingsPopup()
    {
        // ── Monitor picker ────────────────────────────────────────────────────
        MonitorComboBox.Items.Clear();
        MonitorComboBox.Items.Add(new ComboBoxItem
        {
            Content = "(none — use current primary)",
            Tag     = (string?)null,
        });

        IReadOnlyList<MonitorInfo> monitors;
        try { monitors = DisplayManager.GetMonitors(); }
        catch { monitors = []; }

        foreach (var m in monitors)
            MonitorComboBox.Items.Add(new ComboBoxItem { Content = m.DisplayLabel, Tag = m.DeviceName });

        var savedDevice = DisplaySettings?.TargetDevice;
        var deviceIndex = 0;
        for (int i = 1; i < MonitorComboBox.Items.Count; i++)
        {
            if ((MonitorComboBox.Items[i] as ComboBoxItem)?.Tag is string dev
                && string.Equals(dev, savedDevice, StringComparison.OrdinalIgnoreCase))
            {
                deviceIndex = i;
                break;
            }
        }
        MonitorComboBox.SelectedIndex = deviceIndex;

        // ── Method picker ─────────────────────────────────────────────────────
        MethodComboBox.Items.Clear();
        MethodComboBox.Items.Add(new ComboBoxItem { Content = "None (default behavior)",   Tag = DisplaySwitchMethod.None });
        MethodComboBox.Items.Add(new ComboBoxItem { Content = "Set as primary display",    Tag = DisplaySwitchMethod.SetPrimary });
        MethodComboBox.Items.Add(new ComboBoxItem { Content = "Move game window",          Tag = DisplaySwitchMethod.MoveWindow });
        MethodComboBox.Items.Add(new ComboBoxItem { Content = "Set primary, then revert (keeps taskbar)", Tag = DisplaySwitchMethod.SetPrimaryThenRevert });

        var savedMethod = DisplaySettings?.Method ?? DisplaySwitchMethod.None;
        MethodComboBox.SelectedIndex = (int)savedMethod;

        // ── Revert delay stepper ──────────────────────────────────────────────
        RevertDelayTextBox.Text = (DisplaySettings?.RevertDelaySeconds ?? 8).ToString();
        UpdateRevertDelayVisibility();
    }

    private const int MinRevertDelaySeconds = 1;
    private const int MaxRevertDelaySeconds = 60;

    private void MethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateRevertDelayVisibility();

    private void UpdateRevertDelayVisibility()
    {
        var method = (MethodComboBox.SelectedItem as ComboBoxItem)?.Tag is DisplaySwitchMethod m
            ? m : DisplaySwitchMethod.None;
        RevertDelayPanel.Visibility = method == DisplaySwitchMethod.SetPrimaryThenRevert
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RevertDelayTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(char.IsDigit);

    private void RevertDelayMinusButton_Click(object sender, RoutedEventArgs e)
        => StepRevertDelay(-1);

    private void RevertDelayPlusButton_Click(object sender, RoutedEventArgs e)
        => StepRevertDelay(1);

    private void StepRevertDelay(int delta)
    {
        var current = ReadRevertDelaySeconds();
        RevertDelayTextBox.Text = Math.Clamp(current + delta, MinRevertDelaySeconds, MaxRevertDelaySeconds).ToString();
    }

    private int ReadRevertDelaySeconds() =>
        Math.Clamp(
            int.TryParse(RevertDelayTextBox.Text, out var value) ? value : 8,
            MinRevertDelaySeconds, MaxRevertDelaySeconds);

    private void MonitorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // MethodComboBox is populated after MonitorComboBox during PopulateDisplaySettingsPopup,
        // so ignore selection changes that fire before it has items (initial population).
        if (MethodComboBox.Items.Count == 0) return;

        var pickedRealMonitor = (MonitorComboBox.SelectedItem as ComboBoxItem)?.Tag is string;
        var methodStillNone   = (MethodComboBox.SelectedItem as ComboBoxItem)?.Tag is DisplaySwitchMethod.None;

        // Picking a monitor with the method left at "None" would silently do nothing
        // (None means "no override"), so nudge the method to a sane default.
        if (pickedRealMonitor && methodStillNone)
            MethodComboBox.SelectedIndex = (int)DisplaySwitchMethod.SetPrimary;
    }

    private void SaveDisplayButton_Click(object sender, RoutedEventArgs e)
    {
        var targetDevice  = (MonitorComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
        var method        = (MethodComboBox.SelectedItem as ComboBoxItem)?.Tag is DisplaySwitchMethod m
                            ? m : DisplaySwitchMethod.None;

        DisplaySettings = new GameDisplaySettings
        {
            TargetDevice      = targetDevice,
            Method            = method,
            RevertDelaySeconds = ReadRevertDelaySeconds(),
        };

        if (Game is not null)
            DisplaySettingsChanged?.Invoke(this, (Game, DisplaySettings));

        DisplaySettingsPopup.IsOpen = false;
    }

    private void ClearDisplayButton_Click(object sender, RoutedEventArgs e)
    {
        DisplaySettings = new GameDisplaySettings(); // defaults: None, null

        if (Game is not null)
            DisplaySettingsChanged?.Invoke(this, (Game, DisplaySettings));

        DisplaySettingsPopup.IsOpen = false;
    }
}
