using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using EnigmaLauncher.Stores;

namespace EnigmaLauncher.UI.Controls;

public partial class GameCard : UserControl
{
    public GameInfo?    Game  { get; private set; }
    public AccountInfo? Owner { get; private set; }

    /// <summary>Raised when the user clicks Play or double-clicks the card.</summary>
    public event EventHandler<GameInfo>? PlayRequested;

    /// <summary>Raised when the user clicks Create Desktop Shortcut.</summary>
    public event EventHandler<GameInfo>? ShortcutRequested;

    /// <summary>Raised when the user clicks Create a shortcut (choose location).</summary>
    public event EventHandler<GameInfo>? ShortcutLocationRequested;

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
}
