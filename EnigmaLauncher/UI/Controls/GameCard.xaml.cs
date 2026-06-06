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
    public GameDisplaySettings? DisplaySettings { get; set; }

    /// <summary>Raised when the user clicks Play or double-clicks the card.</summary>
    public event EventHandler<GameInfo>? PlayRequested;

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
        }
    }

    public void SetArtwork(BitmapImage image)
    {
        ArtworkImage.Source     = image;
        ArtworkImage.Visibility = Visibility.Visible;
        Placeholder.Visibility  = Visibility.Collapsed;
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (Game is not null)
            PlayRequested?.Invoke(this, Game);
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

        var savedMethod = DisplaySettings?.Method ?? DisplaySwitchMethod.None;
        MethodComboBox.SelectedIndex = (int)savedMethod;
    }

    private void SaveDisplayButton_Click(object sender, RoutedEventArgs e)
    {
        var targetDevice  = (MonitorComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
        var method        = (MethodComboBox.SelectedItem as ComboBoxItem)?.Tag is DisplaySwitchMethod m
                            ? m : DisplaySwitchMethod.None;

        DisplaySettings = new GameDisplaySettings { TargetDevice = targetDevice, Method = method };

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
