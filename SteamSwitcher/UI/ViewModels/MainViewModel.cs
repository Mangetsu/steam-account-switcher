using System.ComponentModel;
using System.Runtime.CompilerServices;
using SteamSwitcher.Steam;

namespace SteamSwitcher.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private SteamAccount? _currentAccount;
    private List<GameEntry> _games = [];
    private List<SteamAccount> _accounts = [];
    private string _statusText = "Loading...";
    private bool _isLoading = true;

    public SteamAccount? CurrentAccount
    {
        get => _currentAccount;
        set { _currentAccount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentAccountDisplay)); }
    }

    public string CurrentAccountDisplay => _currentAccount is not null
        ? $"{_currentAccount.PersonaName} ({_currentAccount.AccountName})"
        : "Not signed in";

    public List<GameEntry> Games
    {
        get => _games;
        set { _games = value; OnPropertyChanged(); }
    }

    public List<SteamAccount> Accounts
    {
        get => _accounts;
        set { _accounts = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
