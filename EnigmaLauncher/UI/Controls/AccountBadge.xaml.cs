using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EnigmaLauncher.Stores;

namespace EnigmaLauncher.UI.Controls;

public partial class AccountBadge : UserControl
{
    private const string DefaultColor = "#6B7280";

    public static readonly DependencyProperty AccountProperty =
        DependencyProperty.Register(nameof(Account), typeof(AccountInfo), typeof(AccountBadge),
            new PropertyMetadata(null, OnAccountChanged));

    public AccountInfo? Account
    {
        get => (AccountInfo?)GetValue(AccountProperty);
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
            BadgeBorder.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(DefaultColor));
            return;
        }

        BadgeText.Text = Account.DisplayName.Length > 0
            ? Account.DisplayName : Account.AccountName;

        var colorHex = string.IsNullOrEmpty(Account.BadgeColor)
            ? DefaultColor : Account.BadgeColor;
        BadgeBorder.Background = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(colorHex));
    }

    /// <summary>
    /// Returns the badge colour for an <see cref="AccountInfo"/>.
    /// Convenience helper for code that needs the colour without a control instance.
    /// </summary>
    public static string GetColorForAccount(AccountInfo? account) =>
        account?.BadgeColor ?? DefaultColor;
}
