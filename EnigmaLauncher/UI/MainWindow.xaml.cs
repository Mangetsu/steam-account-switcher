using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using EnigmaLauncher.Steam;
using EnigmaLauncher.Shortcuts;
using EnigmaLauncher.UI.Controls;

namespace EnigmaLauncher.UI;

public partial class MainWindow : Window
{
    private readonly SteamConfig _config;
    private readonly AccountManager _accounts;
    private readonly LibraryScanner _scanner;
    private readonly ArtworkResolver _artwork;
    private readonly ShortcutCreator _shortcuts;

    // All loaded cards for filtering
    private readonly List<(GameCard Card, GameEntry Game, SteamAccount? Owner)> _allCards = [];
    private List<SteamAccount> _loadedAccounts = [];
    private string _activeFilter = "all";

    public MainWindow(SteamConfig config)
    {
        _config = config;
        _accounts = new AccountManager(config);
        _scanner = new LibraryScanner(config);
        _artwork = new ArtworkResolver(config);
        _shortcuts = new ShortcutCreator();

        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await LoadData();

    private async Task LoadData()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        GameGrid.Children.Clear();
        _allCards.Clear();

        List<GameEntry> games;
        List<SteamAccount> accounts;
        SteamAccount? current;

        try
        {
            LoadingSubText.Text = "Reading Steam accounts...";
            accounts = await Task.Run(() => _accounts.LoadAccounts());
            current  = await Task.Run(() => _accounts.GetCurrentAccount());

            LoadingSubText.Text = "Scanning game libraries...";
            games = await Task.Run(() => _scanner.Scan());
        }
        catch (Exception ex)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            MessageBox.Show($"Failed to load Steam data:\n\n{ex.Message}",
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
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        var accountLookup = accounts.ToDictionary(a => a.SteamId64);

        foreach (var game in games)
        {
            accountLookup.TryGetValue(game.LastOwnerSteamId64, out var owner);
            var card = new GameCard();
            card.Initialize(game, owner);
            card.PlayRequested             += OnPlayRequested;
            card.ShortcutRequested         += OnShortcutRequested;
            card.ShortcutLocationRequested += OnShortcutLocationRequested;
            card.Margin = new Thickness(8);
            _allCards.Add((card, game, owner));
        }

        ApplyFilter();
        LoadingOverlay.Visibility = Visibility.Collapsed;

        _ = LoadArtworkAsync([.. _allCards]);
    }

    // ── Account header ────────────────────────────────────────────────────────

    private void RefreshAccountHeader(SteamAccount? account)
    {
        if (account is null)
        {
            CurrentAccountText.Text = "Not signed in";
            CurrentAccountBadge.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#6B7280"));
            return;
        }

        CurrentAccountText.Text = account.PersonaName.Length > 0
            ? account.PersonaName
            : account.AccountName;

        var colorHex = AccountBadge.GetColorForAccount(account.AccountName);
        CurrentAccountBadge.Background = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(colorHex));
    }

    // ── Account switcher popup ────────────────────────────────────────────────

    private void AccountSwitchButton_Click(object sender, RoutedEventArgs e)
    {
        AccountSwitcherPopup.IsOpen = !AccountSwitcherPopup.IsOpen;
    }

    private void BuildAccountSwitcherPopup(SteamAccount? current)
    {
        AccountSwitcherPanel.Children.Clear();

        var others = _loadedAccounts
            .Where(a => current is null || a.SteamId64 != current.SteamId64)
            .ToList();

        if (others.Count == 0)
        {
            AccountSwitcherPanel.Children.Add(new TextBlock
            {
                Text = "No other accounts",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8BA6B5")),
                FontSize = 12,
                Margin = new Thickness(10, 8, 10, 8),
            });
            return;
        }

        foreach (var account in others)
        {
            var label    = account.PersonaName.Length > 0 ? account.PersonaName : account.AccountName;
            var colorHex = AccountBadge.GetColorForAccount(account.AccountName);
            var color    = (Color)ColorConverter.ConvertFromString(colorHex);

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

        if (sender is not Button { Tag: SteamAccount target }) return;

        OpenLaunchWindow(SteamOperations.SwitchAccount(_config, target));
    }

    // ── Filter buttons ────────────────────────────────────────────────────────

    private void BuildFilterButtons(List<SteamAccount> accounts)
    {
        FilterPanel.Children.Clear();
        AddFilterButton("All", "all");
        foreach (var account in accounts)
        {
            var label = account.PersonaName.Length > 0 ? account.PersonaName : account.AccountName;
            AddFilterButton(label, account.AccountName);
        }
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
        foreach (var (card, _, owner) in _allCards)
        {
            bool show = _activeFilter == "all"
                || (owner is not null
                    && string.Equals(owner.AccountName, _activeFilter, StringComparison.OrdinalIgnoreCase));
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

    private async Task LoadArtworkAsync(List<(GameCard Card, GameEntry Game, SteamAccount? Owner)> cards)
    {
        foreach (var (card, game, _) in cards)
        {
            try
            {
                var localPath = await Task.Run(() => _artwork.GetLocalArtworkPath(game.AppId))
                             ?? await Task.Run(() => _artwork.GetCachedDownloadPath(game.AppId));

                if (localPath is null)
                {
                    _ = Task.Run(async () =>
                    {
                        var downloaded = await _artwork.DownloadArtworkAsync(game.AppId);
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
            bmp.UriSource       = new Uri(imagePath, UriKind.Absolute);
            bmp.CacheOption     = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 200;
            bmp.EndInit();
            bmp.Freeze();
            card.SetArtwork(bmp);
        }
        catch { }
    }

    // ── Game actions ──────────────────────────────────────────────────────────

    private void OnPlayRequested(object? sender, GameEntry game)
    {
        OpenLaunchWindow(SteamOperations.LaunchGame(_config, game.AppId, game.LastOwnerSteamId64));
    }

    private void OnShortcutRequested(object? sender, GameEntry game)
    {
        try
        {
            var artworkPath = GetShortcutArtworkPath(game);
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

    private void OnShortcutLocationRequested(object? sender, GameEntry game)
    {
        var dialog = new OpenFolderDialog
        {
            Title = $"Choose where to create a shortcut for {game.Name}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var artworkPath = GetShortcutArtworkPath(game);
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

    private string? GetShortcutArtworkPath(GameEntry game)
    {
        return _artwork.GetLocalArtworkPath(game.AppId)
            ?? _artwork.GetCachedDownloadPath(game.AppId);
    }

    private string? GetMultiOwnerShortcutSuffix(GameEntry game)
    {
        var ownerCount = _allCards
            .Where(item => item.Game.AppId == game.AppId)
            .Select(item => item.Game.LastOwnerSteamId64)
            .Distinct()
            .Count();

        if (ownerCount <= 1) return null;

        var owner = _allCards.FirstOrDefault(item =>
            item.Game.AppId == game.AppId
            && item.Game.LastOwnerSteamId64 == game.LastOwnerSteamId64).Owner;

        var name = owner?.ToString();
        return string.IsNullOrWhiteSpace(name)
            ? game.LastOwnerSteamId64 == 0 ? null : game.LastOwnerSteamId64.ToString()
            : name;
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
            var current = _accounts.GetCurrentAccount();
            RefreshAccountHeader(current);
            BuildAccountSwitcherPopup(current);
        };
        win.Show();
    }

    // ── Toolbar buttons ───────────────────────────────────────────────────────

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var win = new AboutWindow { Owner = this };
        win.ShowDialog();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _accounts.InvalidateCache();
        _activeFilter = "all";
        await LoadData();
    }
}
