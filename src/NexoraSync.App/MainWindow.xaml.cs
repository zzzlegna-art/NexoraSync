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
    private static readonly Dictionary<string, (string Dark, string Light)> ThemePalette = new()
    {
        ["Theme_090E18"] = ("#090E18", "#F3F6FB"),
        ["Theme_0D1421"] = ("#0D1421", "#FFFFFF"),
        ["Theme_111C2B"] = ("#111C2B", "#FFFFFF"),
        ["Theme_152235"] = ("#152235", "#F0F4F9"),
        ["Theme_233249"] = ("#233249", "#DCE4EE"),
        ["Theme_8797AE"] = ("#8797AE", "#68788E"),
        ["Theme_F2F6FC"] = ("#F2F6FC", "#172437"),
        ["Theme_4C9EFF"] = ("#4C9EFF", "#2678D1"),
        ["Theme_9AAAC0"] = ("#9AAAC0", "#5B6B80"),
        ["Theme_192A40"] = ("#192A40", "#EAF2FB"),
        ["Theme_FFFFFF"] = ("#FFFFFF", "#FFFFFF"),
        ["Theme_203B5D"] = ("#203B5D", "#DCEBFA"),
        ["Theme_398BEE"] = ("#398BEE", "#2678D1"),
        ["Theme_57A2FF"] = ("#57A2FF", "#398BEE"),
        ["Theme_1B2A3E"] = ("#1B2A3E", "#EDF2F8"),
        ["Theme_DCE8F8"] = ("#DCE8F8", "#24364C"),
        ["Theme_0B1421"] = ("#0B1421", "#F4F7FB"),
        ["Theme_EAF1FB"] = ("#EAF1FB", "#24364C"),
        ["Theme_2A3B52"] = ("#2A3B52", "#D5DFEA"),
        ["Theme_1B293B"] = ("#1B293B", "#E2E9F1"),
        ["Theme_183B62"] = ("#183B62", "#DDEBFA"),
        ["Theme_2864A1"] = ("#2864A1", "#B9D8F5"),
        ["Theme_70B8FF"] = ("#70B8FF", "#2678D1"),
        ["Theme_F4F8FF"] = ("#F4F8FF", "#172437"),
        ["Theme_60AFFF"] = ("#60AFFF", "#2678D1"),
        ["Theme_61738C"] = ("#61738C", "#65758B"),
        ["Theme_172A40"] = ("#172A40", "#E7F0FB"),
        ["Theme_111F30"] = ("#111F30", "#F4F7FB"),
        ["Theme_263A53"] = ("#263A53", "#D5DFEA"),
        ["Theme_6E829C"] = ("#6E829C", "#68788E"),
        ["Theme_EAF2FD"] = ("#EAF2FD", "#24364C"),
        ["Theme_8498B2"] = ("#8498B2", "#68788E"),
        ["Theme_536780"] = ("#536780", "#6D7E93"),
        ["Theme_D5A85A"] = ("#D5A85A", "#986312"),
        ["Theme_899BB3"] = ("#899BB3", "#68788E"),
        ["Theme_172536"] = ("#172536", "#EEF3F9"),
        ["Theme_2B405A"] = ("#2B405A", "#D5DFEA"),
        ["Theme_E3B45F"] = ("#E3B45F", "#A36A12"),
        ["Theme_D7E2F1"] = ("#D7E2F1", "#24364C"),
        ["Theme_10243A"] = ("#10243A", "#E8F2FC"),
        ["Theme_234B72"] = ("#234B72", "#BDD9F3"),
        ["Theme_72B9FF"] = ("#72B9FF", "#2678D1"),
        ["Theme_A8BED7"] = ("#A8BED7", "#3E526B"),
        ["Theme_17395A"] = ("#17395A", "#E1EFFC"),
        ["Theme_2C659B"] = ("#2C659B", "#B8D7F3"),
        ["Theme_3F91E5"] = ("#3F91E5", "#2678D1"),
        ["Theme_76B9FF"] = ("#76B9FF", "#2678D1"),
        ["Theme_D9EEFF"] = ("#D9EEFF", "#1D3854"),
        ["Theme_8295AF"] = ("#8295AF", "#68788E"),
        ["Theme_F1F6FD"] = ("#F1F6FD", "#172437"),
        ["Theme_7D91AA"] = ("#7D91AA", "#68788E"),
        ["Theme_D9AD61"] = ("#D9AD61", "#986312"),
        ["Theme_70849F"] = ("#70849F", "#68788E"),
        ["Theme_8498B1"] = ("#8498B1", "#68788E"),
        ["Theme_DCE7F5"] = ("#DCE7F5", "#24364C"),
        ["Theme_7488A2"] = ("#7488A2", "#68788E"),
        ["Theme_26374D"] = ("#26374D", "#D5DFEA"),
        ["Theme_778BA5"] = ("#778BA5", "#68788E"),
        ["Theme_7F93AC"] = ("#7F93AC", "#68788E"),
        ["Theme_18283C"] = ("#18283C", "#EDF2F8"),
        ["Theme_D8B16C"] = ("#D8B16C", "#986312"),
        ["Theme_EAF2FC"] = ("#EAF2FC", "#24364C"),
        ["Theme_79D8B0"] = ("#79D8B0", "#16845D"),
        ["Theme_91A4BC"] = ("#91A4BC", "#5B6D83"),
        ["Theme_352B1D"] = ("#352B1D", "#FFF4DF"),
        ["Theme_E7C58D"] = ("#E7C58D", "#82591D"),
        ["Theme_9CB0C9"] = ("#9CB0C9", "#5B6D83"),
        ["Theme_NavHoverText"] = ("#FFFFFF", "#172437"),
    };
    private bool _isEnglish;
    private string _activePanel = "overview";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private const string CurrentVersion = "0.1.5";
    private string _selectedFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "NexoraSync", "Shared");

    public MainWindow()
    {
        InitializeComponent();
        LoadPreferences();
        ApplyTheme(false);
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
            if (values.TryGetValue("theme", out var theme)) _lightTheme = theme.Equals("light", StringComparison.OrdinalIgnoreCase);
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
                $"language={(_isEnglish ? "en" : "es")}",
                $"theme={(_lightTheme ? "light" : "dark")}"
            });
            FolderStatus.Text = "Folder saved on this computer";
            FolderStatusSettings.Text = FolderStatus.Text;
            SaveStatus.Text = "Preferences saved successfully.";
        }
        catch (Exception ex) { SaveStatus.Text = "Could not save preferences: " + ex.Message; }
    }

    private void ShowPanel(string name)
    {
        _activePanel = name;
        UpdateNavigationState();
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

    private void UpdateNavigationState()
    {
        var buttons = new Dictionary<string, System.Windows.Controls.Button>
        {
            ["overview"] = NavOverview, ["folder"] = NavFolder, ["activity"] = NavActivity,
            ["devices"] = NavDevices, ["settings"] = NavSettings, ["guide"] = NavGuide, ["updates"] = NavUpdates
        };
        foreach (var item in buttons)
        {
            item.Value.Background = item.Key == _activePanel
                ? (Brush)FindResource("Theme_172A40") : Brushes.Transparent;
            item.Value.Foreground = item.Key == _activePanel
                ? (Brush)FindResource("Theme_NavSelectedText") : (Brush)FindResource("Theme_9AAAC0");
        }
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
        ApplyTheme(true);
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllLines(SettingsFile, new[]
            {
                $"folder={_selectedFolder}",
                $"server={ServerUrl.Text.Trim()}",
                $"language={(_isEnglish ? "en" : "es")}",
                $"theme={(_lightTheme ? "light" : "dark")}"
            });
        }
        catch { /* Theme changes remain active for this session if preferences cannot be saved. */ }
    }

    private void ApplyTheme(bool animate)
    {
        foreach (var item in ThemePalette)
        {
            if (Resources[item.Key] is not SolidColorBrush brush) continue;
            var color = (Color)ColorConverter.ConvertFromString(_lightTheme ? item.Value.Light : item.Value.Dark);
            if (animate)
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, TimeSpan.FromMilliseconds(240))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
            else
                brush.Color = color;
        }

        Background = (Brush)FindResource("Theme_090E18");
        Root.Background = (Brush)FindResource("Theme_090E18");
        UpdateNavigationState();
        ThemeButton.Content = _lightTheme
            ? (_isEnglish ? "☾   Dark appearance" : "☾   Tema oscuro")
            : (_isEnglish ? "☼   Light appearance" : "☼   Tema claro");
    }
}