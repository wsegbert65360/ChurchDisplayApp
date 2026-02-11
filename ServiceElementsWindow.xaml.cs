using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp;

public partial class ServiceElementsWindow : Window
{
    private static AppSettings _settings = AppSettings.Load();
    private readonly Dictionary<string, string?> _elementPaths = new();
    private readonly MainWindow _mainWindow;

    public ServiceElementsWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        LoadSavedElements();
    }

    private string? GetFullPath(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;
            
        // First try the saved media directory if we have one
        if (!string.IsNullOrEmpty(_settings.LastMediaDirectory))
        {
            var fullPath = Path.Combine(_settings.LastMediaDirectory, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }
            
        // Try to find the file in common media directories
        var searchPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Videos"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Church Media"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Worship Media"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Church"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        };

        foreach (var searchPath in searchPaths)
        {
            var fullPath = Path.Combine(searchPath, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    private void SetElement(string elementKey, string? path)
    {
        var element = ServiceElementDefinition.AllElements.FirstOrDefault(e => e.Key == elementKey);
        if (element == null) return;

        var label = FindName(elementKey + "FileLabel") as TextBlock;
        if (label == null) return;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            _elementPaths[elementKey] = null;
            label.Text = "No file selected";
            label.Foreground = System.Windows.Media.Brushes.Gray;
            label.FontStyle = FontStyles.Italic;
        }
        else
        {
            _elementPaths[elementKey] = path;
            var fileName = Path.GetFileName(path);
            label.Text = fileName;
            label.Foreground = System.Windows.Media.Brushes.Black;
            label.FontStyle = FontStyles.Normal;
            
            // Update settings via reflection
            var prop = _settings.GetType().GetProperty(element.SettingsProperty);
            prop?.SetValue(_settings, fileName);
            _settings.Save();
        }
    }

    private void LoadSavedElements()
    {
        foreach (var element in ServiceElementDefinition.AllElements)
        {
            var prop = _settings.GetType().GetProperty(element.SettingsProperty);
            var fileName = prop?.GetValue(_settings) as string;
            SetElement(element.Key, GetFullPath(fileName));
        }
    }

    private void SelectFile(string elementKey)
    {
        var element = ServiceElementDefinition.AllElements.FirstOrDefault(e => e.Key == elementKey);
        if (element == null) return;

        var dlg = new OpenFileDialog
        {
            Filter = "Media Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma|All Files|*.*",
            Title = $"Select {element.DisplayName}"
        };

        if (dlg.ShowDialog() == true)
        {
            SetElement(elementKey, dlg.FileName);
            _settings.LastMediaDirectory = Path.GetDirectoryName(dlg.FileName);
            _settings.Save();
        }
    }

    private void UseElement(string elementKey)
    {
        var element = ServiceElementDefinition.AllElements.FirstOrDefault(e => e.Key == elementKey);
        if (element == null) return;

        if (_elementPaths.TryGetValue(elementKey, out var path) && !string.IsNullOrEmpty(path) && File.Exists(path))
        {
            _mainWindow.PlaylistManager.AddFiles(new[] { path });
            MessageBox.Show($"{element.DisplayName} added to playlist.", "Added to Playlist",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show($"No {element.DisplayName} file selected. Please select a file first.", "No File Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // Generic event handlers using Tag to pass element key
    private void ElementSelect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string key)
            SelectFile(key);
    }

    private void ElementClear_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string key)
            SetElement(key, null);
    }

    private void ElementUse_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string key)
            UseElement(key);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void MakeService_Click(object sender, RoutedEventArgs e)
    {
        var addedCount = 0;
        
        foreach (var element in ServiceElementDefinition.AllElements)
        {
            if (_elementPaths.TryGetValue(element.Key, out var path) && !string.IsNullOrEmpty(path) && File.Exists(path))
            {
                _mainWindow.PlaylistManager.AddFiles(new[] { path });
                addedCount++;
            }
        }
        
        if (addedCount > 0)
        {
            MessageBox.Show($"Service created! Added {addedCount} elements to the playlist.", "Service Created",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("No service elements selected. Please select files for at least one service element.", "No Elements Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
