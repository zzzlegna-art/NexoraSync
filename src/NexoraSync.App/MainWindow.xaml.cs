using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace NexoraSync;

public partial class MainWindow : Window
{
    private bool _lightTheme;
    private string _selectedFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "NexoraSync", "Shared");

    public MainWindow()
    {
        InitializeComponent();
        LoadPreferences();
        FolderPath.Text = _selectedFolder;
        FolderPathSettings.Text = _selectedFolder;
        FolderStatus.Text = Directory.Exists(_selectedFolder)
            ? "Folder available on this PC" : "Choose or create your shared folder";
        FolderStatusSettings.Text = FolderStatus.Text;
    }

    private string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexoraSync");
    private string SettingsFile => Path.Combine(SettingsDirectory, "settings.txt");

    private void LoadPreferences()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return;
            var values = File.ReadAllLines(SettingsFile)
                .Select(line => line.Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);
            if (values.TryGetValue("folder", out var folder) && !string.IsNullOrWhiteSpace(folder))
                _selectedFolder = folder;
            if (values.TryGetValue("server", out var server) && !string.IsNullOrWhiteSpace(server))
                ServerUrl.Text = server;
        }
        catch { /* Retain safe defaults if preferences are unreadable. */ }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose your NEXORA shared folder", Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        _selectedFolder = dialog.FolderName;
        FolderPath.Text = _selectedFolder;
        FolderPathSettings.Text = _selectedFolder;
        FolderStatus.Text = "Folder selected · Save preferences to keep it";
        FolderStatusSettings.Text = FolderStatus.Text;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_selectedFolder);
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllLines(SettingsFile, new[]
            {
                $"folder={_selectedFolder}",
                $"server={ServerUrl.Text.Trim()}"
            });
            FolderStatus.Text = "Folder saved on this computer";
            FolderStatusSettings.Text = FolderStatus.Text;
            SaveStatus.Text = "Preferences saved successfully.";
        }
        catch (Exception ex) { SaveStatus.Text = "Could not save preferences: " + ex.Message; }
    }

    private void ShowPanel(string name)
    {
        OverviewPanel.Visibility = name == "overview" ? Visibility.Visible : Visibility.Collapsed;
        FolderPanel.Visibility = name == "folder" ? Visibility.Visible : Visibility.Collapsed;
        ActivityPanel.Visibility = name == "activity" ? Visibility.Visible : Visibility.Collapsed;
        DevicesPanel.Visibility = name == "devices" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = name == "settings" ? Visibility.Visible : Visibility.Collapsed;
        (PageTitle.Text, PageSubtitle.Text) = name switch
        {
            "folder" => ("Shared folder", "Choose the local home for your shared files."),
            "activity" => ("Activity", "Track changes and events across your workspace."),
            "devices" => ("Devices", "Manage the computers connected to your private workspace."),
            "settings" => ("Settings", "Control your connection and local preferences."),
            _ => ("Overview", "Your synchronization workspace at a glance.")
        };
    }

    private void NavOverview_Click(object sender, RoutedEventArgs e) => ShowPanel("overview");
    private void NavFolder_Click(object sender, RoutedEventArgs e) => ShowPanel("folder");
    private void NavActivity_Click(object sender, RoutedEventArgs e) => ShowPanel("activity");
    private void NavDevices_Click(object sender, RoutedEventArgs e) => ShowPanel("devices");
    private void NavSettings_Click(object sender, RoutedEventArgs e) => ShowPanel("settings");

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _lightTheme = !_lightTheme;
        ChangeBrush("Page", _lightTheme ? "#F1F5FA" : "#090E18");
        ChangeBrush("Sidebar", _lightTheme ? "#FFFFFF" : "#0D1421");
        ChangeBrush("Panel", _lightTheme ? "#FFFFFF" : "#111C2B");
        ChangeBrush("PanelAlt", _lightTheme ? "#EAF0F7" : "#152235");
        ChangeBrush("Line", _lightTheme ? "#D8E1EC" : "#233249");
        ChangeBrush("Muted", _lightTheme ? "#65758B" : "#8797AE");
        Background = (Brush)FindResource("Page");
        Root.Background = (Brush)FindResource("Page");
        ThemeButton.Content = _lightTheme ? "☾   Dark appearance" : "☼   Light appearance";
    }

    private void ChangeBrush(string key, string color)
    {
        if (Resources[key] is SolidColorBrush brush)
            brush.Color = (Color)ColorConverter.ConvertFromString(color);
    }
}