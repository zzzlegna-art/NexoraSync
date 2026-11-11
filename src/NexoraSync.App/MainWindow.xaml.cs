using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
namespace NexoraSync;
public partial class MainWindow : Window
{
 private bool _light;
 public MainWindow() { InitializeComponent(); }
 private void BrowseFolder_Click(object sender, RoutedEventArgs e)
 {
  using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "Selecciona la carpeta compartida" };
  if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) FolderPath.Text = dialog.SelectedPath;
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
  foreach (var tb in FindVisualChildren<TextBlock>(this))
   if (tb.Foreground is SolidColorBrush b && b.Color == (Color)ColorConverter.ConvertFromString("#FFFFFF"))
    tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_light ? "#172338" : "#FFFFFF"));
 }
 private static IEnumerable<T> FindVisualChildren<T>(DependencyObject dep) where T : DependencyObject
 {
  for (int i=0;i<VisualTreeHelper.GetChildrenCount(dep);i++) {
   var child=VisualTreeHelper.GetChild(dep,i);
   if(child is T t) yield return t;
   foreach(var nested in FindVisualChildren<T>(child)) yield return nested;
  }
 }
}