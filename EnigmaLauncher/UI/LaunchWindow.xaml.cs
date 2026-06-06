using System.Windows;

namespace EnigmaLauncher.UI;

/// <summary>
/// A small floating status window that runs any async operation and shows its
/// progress.  Business logic lives in <see cref="Steam.SteamOperations"/> —
/// this window is deliberately ignorant of what the operation actually does.
/// </summary>
public partial class LaunchWindow : Window
{
    private readonly Func<IProgress<string>?, Task> _operation;

    public LaunchWindow(Func<IProgress<string>?, Task> operation)
    {
        _operation = operation;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await RunOperation();

    private async Task RunOperation()
    {
        UpdateStatus("Initialising...");
        var progress = new Progress<string>(msg => Dispatcher.Invoke(() => UpdateStatus(msg)));
        try
        {
            await _operation(progress);
            // The operation reports its own final message; just linger then close.
            await Task.Delay(2000);
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;
        ErrorText.Visibility  = Visibility.Collapsed;
        ButtonPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
        StatusText.Visibility  = Visibility.Collapsed;
        ErrorText.Text         = message;
        ErrorText.Visibility   = Visibility.Visible;
        ButtonPanel.Visibility = Visibility.Visible;
        RetryButton.Visibility = Visibility.Visible;
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        ButtonPanel.Visibility = Visibility.Collapsed;
        ErrorText.Visibility   = Visibility.Collapsed;
        await RunOperation();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
