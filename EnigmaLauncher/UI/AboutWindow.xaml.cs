using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace EnigmaLauncher.UI;

public partial class AboutWindow : Window
{
    private const string RepositoryUrl = "https://github.com/Mangetsu/steam-account-switcher";

    public AboutWindow()
    {
        InitializeComponent();

        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        VersionText.Text = $"Version {version}";
        RuntimeText.Text = $".NET {Environment.Version}";
        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        InstallPathText.Text = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory.TrimEnd('\\', '/');
        InstallPathText.ToolTip = InstallPathText.Text;
    }

    private void GitHubButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(RepositoryUrl);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }
}
