using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Win32;

namespace NexoraSync;

public partial class MainWindow : Window
{
    private bool _lightTheme;
    private bool _isEnglish;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private const string CurrentVersion = "0.1.5";
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
        LanguageBox.SelectedIndex = _isEnglish ? 1 : 0;
        ApplyLanguage();
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
            if (values.TryGetValue("language", out var language)) _isEnglish = language.Equals("en", StringComparison.OrdinalIgnoreCase);
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
                $"server={ServerUrl.Text.Trim()}",
                $"language={(_isEnglish ? "en" : "es")}"
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
        GuidePanel.Visibility = name == "guide" ? Visibility.Visible : Visibility.Collapsed;
        UpdatesPanel.Visibility = name == "updates" ? Visibility.Visible : Visibility.Collapsed;
        (PageTitle.Text, PageSubtitle.Text) = name switch
        {
            "folder" => (_isEnglish ? "Shared folder" : "Carpeta compartida", _isEnglish ? "Choose the local home for your shared files." : "Selecciona la carpeta local de tus archivos compartidos."),
            "activity" => (_isEnglish ? "Activity" : "Actividad", _isEnglish ? "Track changes and events across your workspace." : "Consulta los cambios y eventos de tu espacio de trabajo."),
            "devices" => (_isEnglish ? "Devices" : "Dispositivos", _isEnglish ? "Manage the computers connected to your private workspace." : "Administra los equipos conectados a tu espacio privado."),
            "settings" => (_isEnglish ? "Settings" : "Configuración", _isEnglish ? "Control your connection and local preferences." : "Controla tu conexión y tus preferencias locales."),
            "guide" => (_isEnglish ? "How to use" : "Cómo utilizarlo", _isEnglish ? "A quick guide to getting started with NEXORA SYNC." : "Una guía rápida para comenzar con NEXORA SYNC."),
            "updates" => (_isEnglish ? "Updates" : "Actualizaciones", _isEnglish ? "Check for new NEXORA SYNC versions." : "Consulta las nuevas versiones de NEXORA SYNC."),
            _ => (_isEnglish ? "Overview" : "Resumen", _isEnglish ? "Your synchronization workspace at a glance." : "Tu espacio de sincronización de un vistazo.")
        };
    }

    private void NavOverview_Click(object sender, RoutedEventArgs e) => ShowPanel("overview");
    private void NavFolder_Click(object sender, RoutedEventArgs e) => ShowPanel("folder");
    private void NavActivity_Click(object sender, RoutedEventArgs e) => ShowPanel("activity");
    private void NavDevices_Click(object sender, RoutedEventArgs e) => ShowPanel("devices");
    private void NavSettings_Click(object sender, RoutedEventArgs e) => ShowPanel("settings");
    private void NavGuide_Click(object sender, RoutedEventArgs e) => ShowPanel("guide");
    private void NavUpdates_Click(object sender, RoutedEventArgs e) => ShowPanel("updates");
    private void LanguageBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedIndex < 0) return;
        _isEnglish = LanguageBox.SelectedIndex == 1;
        ApplyLanguage();
    }
    private void ApplyLanguage()
    {
        if (PageTitle == null) return;
        NavOverview.Content = _isEnglish ? "◫    Overview" : "◫    Resumen";
        NavFolder.Content = _isEnglish ? "▣    Shared folder" : "▣    Carpeta compartida";
        NavActivity.Content = _isEnglish ? "◷    Activity" : "◷    Actividad";
        NavDevices.Content = _isEnglish ? "▤    Devices" : "▤    Dispositivos";
        NavSettings.Content = _isEnglish ? "⚙    Settings" : "⚙    Configuración";
        NavGuide.Content = _isEnglish ? "ⓘ    How to use" : "ⓘ    Cómo utilizarlo";
        NavUpdates.Content = _isEnglish ? "↻    Updates" : "↻    Actualizaciones";
        ThemeButton.Content = _lightTheme ? (_isEnglish ? "☾   Dark appearance" : "☾   Tema oscuro") : (_isEnglish ? "☼   Light appearance" : "☼   Tema claro");
        TranslateTree(Root);
    }

    private void TranslateTree(DependencyObject parent)
    {
        var translations = new Dictionary<string, string>
        {
            ["Carpeta compartida"] = "Shared folder", ["Actividad"] = "Activity", ["Dispositivos"] = "Devices",
            ["Configuración"] = "Settings", ["Resumen"] = "Overview", ["Cómo utilizarlo"] = "How to use",
            ["Actualizaciones"] = "Updates", ["ESPACIO DE TRABAJO"] = "WORKSPACE", ["PREFERENCIAS"] = "PREFERENCES",
            ["BIENVENIDO A TU NUBE PRIVADA"] = "WELCOME TO YOUR PRIVATE CLOUD", ["Todo conectado."] = "Everything, connected.",
            ["Un solo espacio para tus archivos en todos tus dispositivos autorizados."] = "A single space for your files across every authorized device.",
            ["Configurar carpeta compartida   →"] = "Configure shared folder   →", ["DISPOSITIVOS CONECTADOS"] = "CONNECTED DEVICES",
            ["Solo este equipo"] = "This computer only", ["CAMBIOS PENDIENTES"] = "PENDING CHANGES",
            ["Motor de sincronización sin conectar"] = "Sync engine not connected", ["ESTADO DE SINCRONIZACIÓN"] = "SYNC STATUS",
            ["Esperando configuración del servidor"] = "Waiting for server setup", ["Actividad reciente"] = "Recent activity",
            ["Los eventos de tu espacio aparecerán aquí."] = "Your workspace events will appear here.",
            ["Esperando configuración"] = "Waiting for setup", ["Aún no hay actividad de sincronización"] = "No sync activity yet",
            ["Tu espacio compartido"] = "Your shared workspace", ["Selecciona la carpeta local que compartirás con tus dispositivos NEXORA autorizados."] = "Select the local folder that will be shared with your authorized NEXORA devices.",
            ["CARPETA LOCAL"] = "LOCAL FOLDER", ["Explorar carpeta"] = "Browse folder", ["Elegir carpeta"] = "Choose folder",
            ["Guardar preferencias de carpeta"] = "Save folder preferences", ["Centro de actividad"] = "Activity center",
            ["Dispositivos autorizados"] = "Authorized devices", ["Administra los equipos que pueden acceder a tu espacio compartido."] = "Manage computers that can access your shared workspace.",
            ["Este equipo"] = "This computer", ["Preferencias"] = "Preferences", ["Personaliza tu experiencia con NEXORA SYNC."] = "Personalize your NEXORA SYNC experience.",
            ["DIRECCIÓN DEL SERVIDOR"] = "SERVER ADDRESS", ["Guardar preferencias locales"] = "Save local preferences",
            ["Tu espacio de sincronización de un vistazo."] = "Your synchronization workspace at a glance.",
            ["Selecciona la carpeta local de tus archivos compartidos."] = "Choose the local home for your shared files.",
            ["Consulta los cambios y eventos de tu espacio de trabajo."] = "Track changes and events across your workspace.",
            ["Administra los equipos conectados a tu espacio privado."] = "Manage the computers connected to your private workspace.",
            ["Controla tu conexión y tus preferencias locales."] = "Control your connection and local preferences.",
            ["Una guía rápida para comenzar con NEXORA SYNC."] = "A quick guide to getting started with NEXORA SYNC.",
            ["Consulta las nuevas versiones de NEXORA SYNC."] = "Check for new NEXORA SYNC versions.",
            ["Primeros pasos con NEXORA"] = "Getting started with NEXORA", ["Configura tu espacio de trabajo en tres pasos."] = "Set up your workspace in three steps.",
            ["01 · Elige tu carpeta compartida"] = "01 · Choose your shared folder", ["Selecciona la ubicación local donde guardarás tus archivos."] = "Select the local location where your files will live.",
            ["02 · Configura el servidor"] = "02 · Configure the server", ["Cuando el servicio esté disponible, introduce la dirección del servidor en Configuración."] = "When the service is available, enter the server address in Settings.",
            ["03 · Autoriza tus equipos"] = "03 · Authorize your devices", ["Vincula únicamente los computadores que deban acceder a tus archivos."] = "Only pair computers that should access your files.",
            ["Comprueba si hay una versión más reciente."] = "Check whether a newer version is available.",
            ["VERSIÓN INSTALADA"] = "INSTALLED VERSION", ["Buscar actualizaciones"] = "Check for updates",
            ["La descarga e instalación seguirán siendo manuales en esta versión."] = "Downloading and installing updates will remain manual in this version."
        };
        var reverse = translations.ToDictionary(pair => pair.Value, pair => pair.Key);
        string ConvertText(string value) => _isEnglish
            ? (translations.TryGetValue(value, out var translated) ? translated : value)
            : (reverse.TryGetValue(value, out var original) ? original : value);
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < count; index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is System.Windows.Controls.TextBlock textBlock) textBlock.Text = ConvertText(textBlock.Text);
            else if (child is System.Windows.Controls.Button button && button.Content is string content) button.Content = ConvertText(content);
            TranslateTree(child);
        }
    }
    }
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/zzzlegna-art/NexoraSync/releases/latest");
            request.Headers.UserAgent.ParseAdd("NexoraSync-Updater/0.1.5");
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var tag = json.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v');
            if (Version.TryParse(tag, out var latest) && Version.TryParse(CurrentVersion, out var current) && latest > current)
            {
                UpdateStatus.Text = (_isEnglish ? "New version available: " : "Hay una nueva versión disponible: ") + tag;
                MessageBox.Show((_isEnglish ? "A new NEXORA SYNC version is available: " : "Hay una nueva versión de NEXORA SYNC disponible: ") + tag, "NEXORA SYNC", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch { /* Update checks are optional and must never block startup. */ }
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateStatus.Text = _isEnglish ? "Checking official releases..." : "Consultando versiones oficiales...";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/zzzlegna-art/NexoraSync/releases/latest");
            request.Headers.UserAgent.ParseAdd("NexoraSync-Updater/0.1.5");
            using var response = await Http.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) { UpdateStatus.Text = _isEnglish ? "No published release is available yet." : "Aún no hay una versión publicada."; return; }
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var tag = json.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v') ?? CurrentVersion;
            var url = json.RootElement.GetProperty("html_url").GetString();
            if (Version.TryParse(tag, out var latest) && Version.TryParse(CurrentVersion, out var current) && latest > current)
            {
                UpdateStatus.Text = (_isEnglish ? "New version available: " : "Nueva versión disponible: ") + tag;
                if (!string.IsNullOrWhiteSpace(url)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            else UpdateStatus.Text = _isEnglish ? "You already have the latest published version." : "Ya tienes la última versión publicada.";
        }
        catch { UpdateStatus.Text = _isEnglish ? "Could not check for updates. Check your internet connection." : "No se pudieron consultar actualizaciones. Verifica tu conexión."; }
        finally { CheckUpdatesButton.IsEnabled = true; }
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _lightTheme = !_lightTheme;
        AnimateBrush("Page", _lightTheme ? "#F1F5FA" : "#090E18");
        AnimateBrush("Sidebar", _lightTheme ? "#FFFFFF" : "#0D1421");
        AnimateBrush("Panel", _lightTheme ? "#FFFFFF" : "#111C2B");
        AnimateBrush("PanelAlt", _lightTheme ? "#EAF0F7" : "#152235");
        AnimateBrush("Line", _lightTheme ? "#D8E1EC" : "#233249");
        AnimateBrush("Muted", _lightTheme ? "#65758B" : "#8797AE");
        Background = (Brush)FindResource("Page");
        Root.Background = (Brush)FindResource("Page");
        ThemeButton.Content = _lightTheme ? (_isEnglish ? "☾   Dark appearance" : "☾   Tema oscuro") : (_isEnglish ? "☼   Light appearance" : "☼   Tema claro");
    }

    private void AnimateBrush(string key, string color)
    {
        if (Resources[key] is SolidColorBrush brush)
        {
            var target = (Color)ColorConverter.ConvertFromString(color);
            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(target, TimeSpan.FromMilliseconds(280)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        }
    }

    private void ChangeBrush(string key, string color)
    {
        if (Resources[key] is SolidColorBrush brush)
            brush.Color = (Color)ColorConverter.ConvertFromString(color);
    }
}