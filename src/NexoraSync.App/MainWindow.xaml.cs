using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
namespace NexoraSync;
public partial class MainWindow : Window
{
 private bool _light;
 public MainWindow() { InitializeComponent(); }
 private void BrowseFolder_Click(object sender, RoutedEventArgs e)
 {
  var dialog = new OpenFolderDialog { Title = "Selecciona la carpeta compartida", Multiselect = false };
  if (dialog.ShowDialog() == true) FolderPath.Text = dialog.FolderName;
 }
 private void Save_Click(object sender, RoutedEventArgs e)
 {
  try {
   Directory.CreateDirectory(FolderPath.Text);
   var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexoraSync");
   Directory.CreateDirectory(root);
   File.WriteAllText(Path.Combine(root, "settings.txt"), $"folder={FolderPath.Text}{Environment.NewLine}server={ServerUrl.Text}");
   SaveStatus.Text = "Preferencias locales guardadas correctamente.";
  } catch (Exception ex) { SaveStatus.Text = "No se pudo guardar: " + ex.Message; }
 }
 private void ThemeButton_Click(object sender, RoutedEventArgs e)
 {
  _light = !_light;
  Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_light ? "#F3F6FB" : "#0B1220"));
  ThemeButton.Content = _light ? "☾   Cambiar a modo oscuro" : "☼   Cambiar a modo claro";
 }
}