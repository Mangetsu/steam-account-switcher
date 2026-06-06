using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using EnigmaLauncher.Display;
using EnigmaLauncher.Settings;
using EnigmaLauncher.Stores;
using EnigmaLauncher.Shortcuts;
using EnigmaLauncher.UI.Controls;

namespace EnigmaLauncher.UI;

public partial class MainWindow : Window
{
    private readonly StoreRegistry   _registry;
    private readonly IAccountStore?  _accountStore;  // first account-capable store (Steam)
    private readonly ShortcutCreator _shortcuts;
    private readonly SettingsStore   _settingsStore;

    // All loaded cards, kept for filtering and shortcut-suffix resolution
    private readonly List<(GameCard Card, GameInfo Game, AccountInfo? Owner)> _allCards = [];
    private List<AccountInfo> _loadedAccounts = [];
    private string _activeFilter = "all";

    public MainWindow(StoreRegistry registry)
    {
        _registry      = registry;
        _accountStore  = registry.AccountStores.FirstOrDefault();
        _shortcuts     = new ShortcutCreator();
        _settingsStore = new SettingsStore();

        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDisplayButtonLabel();
        await LoadData();
    }

    private async Task LoadData()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        EmptyState.Visibility     = Visibility.Collapsed;
        GameGrid.Children.Clear();
        _allCards.Clear();

        List<GameInfo>    games;
        List<AccountInfo> accounts;
        AccountInfo?      current;

        try
        {
            LoadingSubText.Text = "Reading accounts...";
            accounts = await Task.Run(() => _registry.GetAllAccounts().ToList());
            current  = await Task.Run(() => _registry.GetCurrentAccount());

            LoadingSubText.Text = "Scanning game libraries...";
            games = await Task.Run(() => _registry.ScanAllGames().ToList());
        }
        catch (Exception ex)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            MessageBox.Show($"Failed to load game data:\n\n{ex.Message}",
                "EnigmaLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _loadedAccounts = accounts;

        RefreshAccountHeader(current);
        BuildFilterButtons(accounts);
        BuildAccountSwitcherPopup(current);

        if (games.Count == 0)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            EmptyState.Visibility     = Visibility.Visible;
            return;
        }

        // AccountId (SteamId64 string) → AccountInfo lookup for owner resolution
        var accountLookup = accounts.ToDictionary(a => a.AccountId);

        foreach (var game in games)
        {
            AccountInfo? owner = null;
            if (game.OwnerAccountId is not null)
                accountLookup.TryGetValue(game.OwnerAccountId, out owner);

            var card = new GameCard();
            card.Initialize(game, owner);
            card.DisplaySettings            = _settingsStore.GetOrCreateGameDisplay(game.StoreId, game.GameId);
            card.PlayRequested             += OnPlayRequested;
            card.ShortcutRequested         += OnShortcutRequested;
            card.ShortcutLocationRequested += OnShortcutLocationRequested;
            card.DisplaySettingsChanged    += OnDisplaySettingsChanged;
            card.Margin = new Thickness(8);
            _allCards.Add((card, game, owner));
        }

        ApplyFilter();
        LoadingOverlay.Visibility = Visibility.Collapsed;

        _ = LoadArtworkAsync([.. _allCards]);
    }

    // ── Account header ────────────────────────────────────────────────────────

    private void RefreshAccountHeader(AccountInfo? account)
    {
        if (account is null)
        {
            CurrentAccountText.Text = "Not signed in";
            CurrentAccountBadge.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#6B7280"));
            return;
        }

        CurrentAccountText.Text = account.DisplayName;

        CurrentAccountBadge.Background = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(account.BadgeColor));
    }

    // ── Account switcher popup ────────────────────────────────────────────────

    private void AccountSwitchButton_Click(object sender, RoutedEventArgs e)
    {
        AccountSwitcherPopup.IsOpen = !AccountSwitcherPopup.IsOpen;
    }

    private void BuildAccountSwitcherPopup(AccountInfo? current)
    {
        AccountSwitcherPanel.Children.Clear();

        var others = _loadedAccounts
            .Where(a => current is null || a.AccountId != current.AccountId)
            .ToList();

        if (others.Count == 0)
        {
            AccountSwitcherPanel.Children.Add(new TextBlock
            {
                Text       = "No other accounts",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8BA6B5")),
                FontSize   = 12,
                Margin     = new Thickness(10, 8, 10, 8),
            });
            return;
        }

        foreach (var account in others)
        {
            var label = account.DisplayName;
            var color = (Color)ColorConverter.ConvertFromString(account.BadgeColor);

            // Small coloured initial badge
            var initials = new TextBlock
            {
                Text       = label[..Math.Min(2, label.Length)].ToUpperInvariant(),
                Foreground = Brushes.White,
                FontSize   = 11,
                FontWeight = FontWeights.SemiBold,
            };
            var badge = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding      = new Thickness(6, 2, 6, 2),
                Background   = new SolidColorBrush(color),
                Margin       = new Thickness(0, 0, 8, 0),
                Child        = initials,
            };

            var nameText = new TextBlock
            {
                Text              = label,
                FontSize          = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground        = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C6D4DF")),
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(badge);
            row.Children.Add(nameText);

            var btn = new Button
            {
                Content = row,
                Style   = (Style)FindResource("AccountSwitchItem"),
                Tag     = account,
            };
            btn.Click += OnAccountSwitchItemClick;
            AccountSwitcherPanel.Children.Add(btn);
        }
    }

    private void OnAccountSwitchItemClick(object sender, RoutedEventArgs e)
    {
        AccountSwitcherPopup.IsOpen = false;

        if (sender is not Button { Tag: AccountInfo target }) return;

        var store = _registry.Get(target.StoreId) as IAccountStore;
        if (store is null) return;

        OpenLaunchWindow(store.BuildSwitchOperation(target));
    }

    // ── Filter buttons ────────────────────────────────────────────────────────

    private void BuildFilterButtons(List<AccountInfo> accounts)
    {
        FilterPanel.Children.Clear();
        AddFilterButton("All", "all");
        foreach (var account in accounts)
            AddFilterButton(account.DisplayName, account.AccountId);
        _activeFilter = "all";
        RefreshFilterButtonStyles();
    }

    private void AddFilterButton(string label, string tag)
    {
        var btn = new Button { Content = label, Tag = tag, Margin = new Thickness(0, 0, 6, 0) };
        btn.Click += FilterButton_Click;
        btn.Style  = (Style)FindResource("FilterButton");
        FilterPanel.Children.Add(btn);
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _activeFilter = tag;
            ApplyFilter();
            RefreshFilterButtonStyles();
        }
    }

    private void ApplyFilter()
    {
        GameGrid.Children.Clear();
        foreach (var (card, game, _) in _allCards)
        {
            bool show = _activeFilter == "all"
                || game.OwnerAccountId == _activeFilter;
            if (show)
                GameGrid.Children.Add(card);
        }
        UpdateGameCount();
    }

    private void RefreshFilterButtonStyles()
    {
        foreach (Button btn in FilterPanel.Children.OfType<Button>())
        {
            var isActive = btn.Tag is string t && t == _activeFilter;
            btn.Style = (Style)FindResource(isActive ? "FilterButtonActive" : "FilterButton");
        }
    }

    private void UpdateGameCount()
    {
        int visible = GameGrid.Children.Count;
        int total   = _allCards.Count;
        GameCountText.Text = visible == total ? $"{total} games" : $"{visible} / {total} games";
    }

    // ── Artwork loading ───────────────────────────────────────────────────────

    private async Task LoadArtworkAsync(List<(GameCard Card, GameInfo Game, AccountInfo? Owner)> cards)
    {
        foreach (var (card, game, _) in cards)
        {
            try
            {
                var store = _registry.Get(game.StoreId);
                if (store is null) continue;

                // Capture locals for lambda capture safety
                var capturedStore = store;
                var capturedGame  = game;

                var localPath = await Task.Run(() => capturedStore.GetArtworkPath(capturedGame));

                if (localPath is null)
                {
                    _ = Task.Run(async () =>
                    {
                        var downloaded = await capturedStore.DownloadArtworkAsync(capturedGame);
                        if (downloaded is not null)
                            await Dispatcher.InvokeAsync(() => SetCardArtwork(card, downloaded));
                    });
                    continue;
                }

                await Dispatcher.InvokeAsync(() => SetCardArtwork(card, localPath));
            }
            catch { }
        }
    }

    private static void SetCardArtwork(GameCard card, string imagePath)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource        = new Uri(imagePath, UriKind.Absolute);
            bmp.CacheOption      = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 200;
            bmp.EndInit();
            bmp.Freeze();
            card.SetArtwork(bmp);
        }
        catch { }
    }

    // ── Game actions ──────────────────────────────────────────────────────────

    private void OnPlayRequested(object? sender, GameInfo game)
    {
        var store = _registry.Get(game.StoreId);
        if (store is null) return;

        var displaySettings = _settingsStore.GetOrCreateGameDisplay(game.StoreId, game.GameId);
        var op = ApplyDisplaySettings(store.BuildLaunchOperation(game), displaySettings);
        OpenLaunchWindow(op);
    }

    private void OnDisplaySettingsChanged(object? sender, (GameInfo Game, GameDisplaySettings Settings) e)
    {
        _settingsStore.SetGameDisplay(e.Game.StoreId, e.Game.GameId, e.Settings);
    }

    /// <summary>
    /// Wraps <paramref name="op"/> so that display routing is applied before/after launch.
    /// Returns <paramref name="op"/> unchanged when no override is configured.
    /// </summary>
    private static Func<IProgress<string>?, Task> ApplyDisplaySettings(
        Func<IProgress<string>?, Task> op,
        GameDisplaySettings settings)
    {
        if (settings.Method == DisplaySwitchMethod.None
            || string.IsNullOrEmpty(settings.TargetDevice))
            return op; // nothing to do

        return async progress =>
        {
            if (settings.Method == DisplaySwitchMethod.SetPrimary)
            {
                progress?.Report("Switching primary display...");
                DisplayManager.SetPrimary(settings.TargetDevice);
            }

            await op(progress);

            if (settings.Method == DisplaySwitchMethod.MoveWindow)
            {
                // Fire-and-forget: wait for game window to appear, then move it
                var targetDevice = settings.TargetDevice;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000); // give game time to create its window
                    try { DisplayManager.MoveWindowToMonitor(targetDevice); }
                    catch { /* best-effort */ }
                });
            }
        };
    }

    private void OnShortcutRequested(object? sender, GameInfo game)
    {
        try
        {
            var artworkPath = _registry.Get(game.StoreId)?.GetArtworkPath(game);
            var lnkPath = _shortcuts.CreateGameShortcut(
                game,
                artworkPath,
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                GetMultiOwnerShortcutSuffix(game));
            MessageBox.Show($"Shortcut created on your Desktop:\n{Path.GetFileName(lnkPath)}",
                "EnigmaLauncher", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create shortcut:\n\n{ex.Message}",
                "EnigmaLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnShortcutLocationRequested(object? sender, GameInfo game)
    {
        var dialog = new OpenFolderDialog
        {
            Title            = $"Choose where to create a shortcut for {game.Name}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Multiselect      = false,
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var artworkPath = _registry.Get(game.StoreId)?.GetArtworkPath(game);
            var lnkPath = _shortcuts.CreateGameShortcut(
                game,
                artworkPath,
                dialog.FolderName,
                GetMultiOwnerShortcutSuffix(game));
            MessageBox.Show($"Shortcut created:\n{lnkPath}",
                "EnigmaLauncher", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create shortcut:\n\n{ex.Message}",
                "EnigmaLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Returns a display-name suffix when the same game is owned by multiple accounts,
    /// so each shortcut gets a unique file name.  Returns null when there is only one owner.
    /// </summary>
    private string? GetMultiOwnerShortcutSuffix(GameInfo game)
    {
        var ownerCount = _allCards
            .Where(item => item.Game.GameId == game.GameId && item.Game.StoreId == game.StoreId)
            .Select(item => item.Game.OwnerAccountId)
            .Distinct()
            .Count();

        if (ownerCount <= 1) return null;

        var owner = _allCards.FirstOrDefault(item =>
            item.Game.GameId        == game.GameId
            && item.Game.StoreId        == game.StoreId
            && item.Game.OwnerAccountId == game.OwnerAccountId).Owner;

        return owner?.DisplayName ?? game.OwnerAccountId;
    }

    // ── Shared launch-window helper ───────────────────────────────────────────

    /// <summary>
    /// Opens a <see cref="LaunchWindow"/> for <paramref name="operation"/> and
    /// refreshes the account header + switcher popup when it closes.
    /// </summary>
    private void OpenLaunchWindow(Func<IProgress<string>?, Task> operation)
    {
        var win = new LaunchWindow(operation) { Owner = this };
        win.Closed += (_, _) =>
        {
            var current = _accountStore?.GetCurrentAccount();
            RefreshAccountHeader(current);
            BuildAccountSwitcherPopup(current);
        };
        win.Show();
    }

    // ── Global display switcher ───────────────────────────────────────────────

    private void DisplaySwitchButton_Click(object sender, RoutedEventArgs e)
    {
        BuildDisplaySwitcherPopup();
        DisplaySwitcherPopup.IsOpen = !DisplaySwitcherPopup.IsOpen;
    }

    private void BuildDisplaySwitcherPopup()
    {
        DisplaySwitcherPanel.Children.Clear();

        IReadOnlyList<MonitorInfo> monitors;
        try { monitors = DisplayManager.GetMonitors(); }
        catch
        {
            DisplaySwitcherPanel.Children.Add(new TextBlock
            {
                Text       = "No monitors detected",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8BA6B5")),
                FontSize   = 12,
                Margin     = new Thickness(10, 8, 10, 8),
            });
            return;
        }

        foreach (var monitor in monitors)
        {
            var nameText = new TextBlock
            {
                Text              = monitor.DisplayLabel,
                FontSize          = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground        = monitor.IsPrimary
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF66C0F4"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C6D4DF")),
            };

            var btn = new Button
            {
                Content   = nameText,
                Style     = (Style)FindResource("AccountSwitchItem"),
                Tag       = monitor,
                IsEnabled = !monitor.IsPrimary,
            };
            btn.Click += OnDisplaySwitchItemClick;
            DisplaySwitcherPanel.Children.Add(btn);
        }
    }

    private void OnDisplaySwitchItemClick(object sender, RoutedEventArgs e)
    {
        DisplaySwitcherPopup.IsOpen = false;

        if (sender is not Button { Tag: MonitorInfo target }) return;

        try
        {
            DisplayManager.SetPrimary(target.DeviceName);
            UpdateDisplayButtonLabel();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to switch primary display:\n\n{ex.Message}",
                "EnigmaLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Updates the display pill label to reflect the current primary monitor.</summary>
    private void UpdateDisplayButtonLabel()
    {
        try
        {
            var primary = DisplayManager.GetMonitors().FirstOrDefault(m => m.IsPrimary);
            CurrentDisplayText.Text = primary is not null
                ? primary.DisplayLabel.Split('—')[0].Trim()   // "Display 1"
                : "Display";
        }
        catch
        {
            CurrentDisplayText.Text = "Display";
        }
    }

    // ── Toolbar buttons ───────────────────────────────────────────────────────

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var win = new AboutWindow { Owner = this };
        win.ShowDialog();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var store in _registry.AccountStores)
            store.InvalidateAccountCache();
        _activeFilter = "all";
        await LoadData();
    }
}
