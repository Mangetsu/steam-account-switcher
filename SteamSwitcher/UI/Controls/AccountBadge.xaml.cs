using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SteamSwitcher.Steam;

namespace SteamSwitcher.UI.Controls;

public partial class AccountBadge : UserControl
{
    // Account name → badge color mapping
    private static readonly Dictionary<string, string> AccountColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["themangetsu"] = "#2563EB",
        ["thefanopsis"]  = "#16A34A",
    };

    private const string DefaultColor = "#6B7280";

    public static readonly DependencyProperty AccountProperty =
        DependencyProperty.Register(nameof(Account), typeof(SteamAccount), typeof(AccountBadge),
            new PropertyMetadata(null, OnAccountChanged));

    public SteamAccount? Account
    {
        get => (SteamAccount?)GetValue(AccountProperty);
        set => SetValue(AccountProperty, value);
    }

    public AccountBadge()
    {
        InitializeComponent();
    }

    private static void OnAccountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AccountBadge badge)
            badge.Refresh();
    }

    public void Refresh()
    {
        if (Account is null)
        {
            BadgeText.Text = "Unknown";
            BadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(DefaultColor));
            return;
        }

        BadgeText.Text = Account.PersonaName.Length > 0 ? Account.PersonaName : Account.AccountName;

        var colorHex = AccountColors.TryGetValue(Account.AccountName, out var hex) ? hex : DefaultColor;
        BadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
    }

    public static string GetColorForAccount(string accountName)
    {
        return AccountColors.TryGetValue(accountName, out var hex) ? hex : DefaultColor;
    }
}
