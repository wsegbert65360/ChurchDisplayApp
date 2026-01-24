using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace ChurchDisplayApp;

public partial class ServiceElementsWindow : Window
{
    private static AppSettings _settings = AppSettings.Load();
    private string? _callToWorshipPath;
    private string? _doxologyPath;
    private string? _songForBeginningPath;
    private string? _praiseSongPath;
    private string? _prayerSongPath;
    private string? _communionSongPath;
    private string? _childrensMomentSongPath;
    private string? _invitationSongPath;
    private string? _endingSongPath;
    private readonly MainWindow _mainWindow;

    // Helper to get full path from stored filename
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
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures")
        };

        foreach (var searchPath in searchPaths)
        {
            var fullPath = Path.Combine(searchPath, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        // If not found in standard folders, try to search in a broader way
        // Check if there's a Church-related folder structure
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var additionalPaths = new[]
        {
            Path.Combine(userProfile, "Desktop"),
            Path.Combine(userProfile, "Documents"),
            Path.Combine(userProfile, "Downloads"),
            // Add common church media folder locations
            Path.Combine(userProfile, "Church Media"),
            Path.Combine(userProfile, "Worship Media"),
            Path.Combine(userProfile, "Church"),
        };

        foreach (var searchPath in additionalPaths)
        {
            var fullPath = Path.Combine(searchPath, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        // Last resort: try a recursive search in common locations (limited depth)
        try
        {
            var baseSearchPaths = new[] { userProfile, Environment.GetFolderPath(Environment.SpecialFolder.Desktop) };
            foreach (var basePath in baseSearchPaths)
            {
                var foundPath = SearchFileRecursively(basePath, fileName, 2); // Max 2 levels deep
                if (foundPath != null)
                    return foundPath;
            }
        }
        catch
        {
            // If recursive search fails, just return null
        }

        return null;
    }

    private string? SearchFileRecursively(string rootPath, string fileName, int maxDepth)
    {
        if (maxDepth < 0 || string.IsNullOrEmpty(rootPath))
            return null;

        try
        {
            // Check current directory
            var fullPath = Path.Combine(rootPath, fileName);
            if (File.Exists(fullPath))
                return fullPath;

            // Search subdirectories (limited depth)
            if (maxDepth > 0)
            {
                foreach (var directory in Directory.GetDirectories(rootPath))
                {
                    var result = SearchFileRecursively(directory, fileName, maxDepth - 1);
                    if (result != null)
                        return result;
                }
            }
        }
        catch
        {
            // Ignore access errors
        }

        return null;
    }

    public ServiceElementsWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        LoadSavedElements();
    }

    private void LoadSavedElements()
    {
        // Load Song for Beginning of Church (no default)
        if (!string.IsNullOrEmpty(_settings.SongForBeginningFile))
        {
            _songForBeginningPath = GetFullPath(_settings.SongForBeginningFile);
            if (_songForBeginningPath != null)
            {
                SongForBeginningFileLabel.Text = _settings.SongForBeginningFile;
                SongForBeginningFileLabel.Foreground = System.Windows.Media.Brushes.Black;
                SongForBeginningFileLabel.FontStyle = FontStyles.Normal;
            }
        }
        
        // Load Call to Worship (has default)
        if (!string.IsNullOrEmpty(_settings.CallToWorshipFile))
        {
            _callToWorshipPath = GetFullPath(_settings.CallToWorshipFile);
            if (_callToWorshipPath != null)
            {
                CallToWorshipFileLabel.Text = _settings.CallToWorshipFile;
                CallToWorshipFileLabel.Foreground = System.Windows.Media.Brushes.Black;
                CallToWorshipFileLabel.FontStyle = FontStyles.Normal;
            }
        }
        
        // Load Doxology (no default)
        if (!string.IsNullOrEmpty(_settings.DoxologyFile))
        {
            _doxologyPath = GetFullPath(_settings.DoxologyFile);
            if (_doxologyPath != null)
            {
                DoxologyFileLabel.Text = _settings.DoxologyFile;
                DoxologyFileLabel.Foreground = System.Windows.Media.Brushes.Black;
                DoxologyFileLabel.FontStyle = FontStyles.Normal;
            }
        }
        
        // Load Praise Song (no default)
        if (!string.IsNullOrEmpty(_settings.PraiseSongFile))
        {
            _praiseSongPath = GetFullPath(_settings.PraiseSongFile);
            if (_praiseSongPath != null)
            {
                PraiseSongFileLabel.Text = _settings.PraiseSongFile;
                PraiseSongFileLabel.Foreground = System.Windows.Media.Brushes.Black;
                PraiseSongFileLabel.FontStyle = FontStyles.Normal;
            }
        }
        
        // Load Prayer Song (no default)
        if (!string.IsNullOrEmpty(_settings.PrayerSongFile))
        {
            _prayerSongPath = GetFullPath(_settings.PrayerSongFile);
            if (_prayerSongPath != null)
            {
                PrayerSongFileLabel.Text = _settings.PrayerSongFile;
                PrayerSongFileLabel.Foreground = System.Windows.Media.Brushes.Black;
                PrayerSongFileLabel.FontStyle = FontStyles.Normal;
            }
        }
        
        // Load Communion Song (no default)
        if (!string.IsNullOrEmpty(_settings.CommunionSongFile))
        {
            _communionSongPath = GetFullPath(_settings.CommunionSongFile);
            if (_communionSongPath != null)
            {
                CommunionSongFileLabel.Text = _settings.CommunionSongFile;
                CommunionSongFileLabel.Foreground = System.Windows.Media.Brushes.Black;
                CommunionSongFileLabel.FontStyle = FontStyles.Normal;
            }
        }
        
        // Load Childrens Moment Song (has default)
        if (!string.IsNullOrEmpty(_settings.ChildrensMomentSongFile))
        {
            _childrensMomentSongPath = GetFullPath(_settings.ChildrensMomentSongFile);
            if (_childrensMomentSongPath != null)
            {
                ChildrensMomentSongFileLabel.Text = _settings.ChildrensMomentSongFile;
                ChildrensMomentSongFileLabel.Foreground = System.Windows.Media.Brushes.Black;
                ChildrensMomentSongFileLabel.FontStyle = FontStyles.Normal;
            }
        }
        
        // Load Invitation Song (no default)
        if (!string.IsNullOrEmpty(_settings.InvitationSongFile))
        {
            _invitationSongPath = GetFullPath(_settings.InvitationSongFile);
            if (_invitationSongPath != null)
            {
                InvitationSongFileLabel.Text = _settings.InvitationSongFile;
                InvitationSongFileLabel.Foreground = System.Windows.Media.Brushes.Black;
                InvitationSongFileLabel.FontStyle = FontStyles.Normal;
            }
        }
        
        // Load Ending Song (no default)
        if (!string.IsNullOrEmpty(_settings.EndingSongFile))
        {
            _endingSongPath = GetFullPath(_settings.EndingSongFile);
            if (_endingSongPath != null)
            {
                EndingSongFileLabel.Text = _settings.EndingSongFile;
                EndingSongFileLabel.Foreground = System.Windows.Media.Brushes.Black;
                EndingSongFileLabel.FontStyle = FontStyles.Normal;
            }
        }
    }

    private void CallToWorshipSelect_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Media Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma|All Files|*.*",
            Title = "Select Call to Worship File"
        };

        if (dlg.ShowDialog() == true)
        {
            _callToWorshipPath = dlg.FileName;
            var fileName = System.IO.Path.GetFileName(_callToWorshipPath);
            _settings.CallToWorshipFile = fileName;
            _settings.LastMediaDirectory = System.IO.Path.GetDirectoryName(_callToWorshipPath);
            _settings.Save();

            CallToWorshipFileLabel.Text = fileName;
            CallToWorshipFileLabel.Foreground = System.Windows.Media.Brushes.Black;
            CallToWorshipFileLabel.FontStyle = FontStyles.Normal;
        }
    }

    private void SongForBeginningSelect_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Media Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma|All Files|*.*",
            Title = "Select Song for Beginning of Church"
        };

        if (dlg.ShowDialog() == true)
        {
            _songForBeginningPath = dlg.FileName;
            var fileName = System.IO.Path.GetFileName(_songForBeginningPath);
            _settings.SongForBeginningFile = fileName;
            _settings.Save();

            SongForBeginningFileLabel.Text = fileName;
            SongForBeginningFileLabel.Foreground = System.Windows.Media.Brushes.Black;
            SongForBeginningFileLabel.FontStyle = FontStyles.Normal;
        }
    }

    private void SongForBeginningUse_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_songForBeginningPath) && File.Exists(_songForBeginningPath))
        {
            // Add directly to the main window's playlist
            _mainWindow.PlaylistListBox.Items.Add(_songForBeginningPath);
            
            MessageBox.Show("Song for Beginning of Church added to playlist.", "Added to Playlist",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("No Song for Beginning of Church file selected. Please select a file first.", "No File Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void SongForBeginningClear_Click(object sender, RoutedEventArgs e)
    {
        _songForBeginningPath = null;
        _settings.SongForBeginningFile = null;
        _settings.Save();

        SongForBeginningFileLabel.Text = "No file selected";
        SongForBeginningFileLabel.Foreground = System.Windows.Media.Brushes.Gray;
        SongForBeginningFileLabel.FontStyle = FontStyles.Italic;
    }

    private void CallToWorshipUse_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_callToWorshipPath) && File.Exists(_callToWorshipPath))
        {
            // Add directly to the main window's playlist
            _mainWindow.PlaylistListBox.Items.Add(_callToWorshipPath);
            
            MessageBox.Show("Call to Worship added to playlist.", "Added to Playlist",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("No Call to Worship file selected. Please select a file first.", "No File Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CallToWorshipClear_Click(object sender, RoutedEventArgs e)
    {
        _callToWorshipPath = null;
        _settings.CallToWorshipFile = null;
        _settings.Save();

        CallToWorshipFileLabel.Text = "No file selected";
        CallToWorshipFileLabel.Foreground = System.Windows.Media.Brushes.Gray;
        CallToWorshipFileLabel.FontStyle = FontStyles.Italic;
    }

    // Doxology Event Handlers
    private void DoxologySelect_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Media Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma|All Files|*.*",
            Title = "Select Doxology"
        };

        if (dlg.ShowDialog() == true)
        {
            _doxologyPath = dlg.FileName;
            var fileName = System.IO.Path.GetFileName(_doxologyPath);
            _settings.DoxologyFile = fileName;
            _settings.LastMediaDirectory = System.IO.Path.GetDirectoryName(_doxologyPath);
            _settings.Save();

            DoxologyFileLabel.Text = fileName;
            DoxologyFileLabel.Foreground = System.Windows.Media.Brushes.Black;
            DoxologyFileLabel.FontStyle = FontStyles.Normal;
        }
    }

    private void DoxologyClear_Click(object sender, RoutedEventArgs e)
    {
        _doxologyPath = null;
        _settings.DoxologyFile = null;
        _settings.Save();

        DoxologyFileLabel.Text = "No file selected";
        DoxologyFileLabel.Foreground = System.Windows.Media.Brushes.Gray;
        DoxologyFileLabel.FontStyle = FontStyles.Italic;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.DialogResult = false;
        this.Close();
    }

    // Praise Song Event Handlers
    private void PraiseSongSelect_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Media Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma|All Files|*.*",
            Title = "Select Praise Song"
        };

        if (dlg.ShowDialog() == true)
        {
            _praiseSongPath = dlg.FileName;
            var fileName = System.IO.Path.GetFileName(_praiseSongPath);
            _settings.PraiseSongFile = fileName;
            _settings.Save();

            PraiseSongFileLabel.Text = fileName;
            PraiseSongFileLabel.Foreground = System.Windows.Media.Brushes.Black;
            PraiseSongFileLabel.FontStyle = FontStyles.Normal;
        }
    }

    private void PraiseSongClear_Click(object sender, RoutedEventArgs e)
    {
        _praiseSongPath = null;
        _settings.PraiseSongFile = null;
        _settings.Save();

        PraiseSongFileLabel.Text = "No file selected";
        PraiseSongFileLabel.Foreground = System.Windows.Media.Brushes.Gray;
        PraiseSongFileLabel.FontStyle = FontStyles.Italic;
    }

    // Prayer Song Event Handlers
    private void PrayerSongSelect_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Media Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma|All Files|*.*",
            Title = "Select Prayer Song"
        };

        if (dlg.ShowDialog() == true)
        {
            _prayerSongPath = dlg.FileName;
            var fileName = System.IO.Path.GetFileName(_prayerSongPath);
            _settings.PrayerSongFile = fileName;
            _settings.Save();

            PrayerSongFileLabel.Text = fileName;
            PrayerSongFileLabel.Foreground = System.Windows.Media.Brushes.Black;
            PrayerSongFileLabel.FontStyle = FontStyles.Normal;
        }
    }

    private void PrayerSongClear_Click(object sender, RoutedEventArgs e)
    {
        _prayerSongPath = null;
        _settings.PrayerSongFile = null;
        _settings.Save();

        PrayerSongFileLabel.Text = "No file selected";
        PrayerSongFileLabel.Foreground = System.Windows.Media.Brushes.Gray;
        PrayerSongFileLabel.FontStyle = FontStyles.Italic;
    }

    // Communion Song Event Handlers
    private void CommunionSongSelect_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Media Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma|All Files|*.*",
            Title = "Select Communion Song"
        };

        if (dlg.ShowDialog() == true)
        {
            _communionSongPath = dlg.FileName;
            var fileName = System.IO.Path.GetFileName(_communionSongPath);
            _settings.CommunionSongFile = fileName;
            _settings.Save();

            CommunionSongFileLabel.Text = fileName;
            CommunionSongFileLabel.Foreground = System.Windows.Media.Brushes.Black;
            CommunionSongFileLabel.FontStyle = FontStyles.Normal;
        }
    }

    private void CommunionSongClear_Click(object sender, RoutedEventArgs e)
    {
        _communionSongPath = null;
        _settings.CommunionSongFile = null;
        _settings.Save();

        CommunionSongFileLabel.Text = "No file selected";
        CommunionSongFileLabel.Foreground = System.Windows.Media.Brushes.Gray;
        CommunionSongFileLabel.FontStyle = FontStyles.Italic;
    }

    // Childrens Moment Song Event Handlers
    private void ChildrensMomentSongSelect_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Media Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma|All Files|*.*",
            Title = "Select Childrens Moment Song"
        };

        if (dlg.ShowDialog() == true)
        {
            _childrensMomentSongPath = dlg.FileName;
            var fileName = System.IO.Path.GetFileName(_childrensMomentSongPath);
            _settings.ChildrensMomentSongFile = fileName;
            _settings.LastMediaDirectory = System.IO.Path.GetDirectoryName(_childrensMomentSongPath);
            _settings.Save();

            ChildrensMomentSongFileLabel.Text = fileName;
            ChildrensMomentSongFileLabel.Foreground = System.Windows.Media.Brushes.Black;
            ChildrensMomentSongFileLabel.FontStyle = FontStyles.Normal;
        }
    }

    private void ChildrensMomentSongClear_Click(object sender, RoutedEventArgs e)
    {
        _childrensMomentSongPath = null;
        _settings.ChildrensMomentSongFile = null;
        _settings.Save();

        ChildrensMomentSongFileLabel.Text = "No file selected";
        ChildrensMomentSongFileLabel.Foreground = System.Windows.Media.Brushes.Gray;
        ChildrensMomentSongFileLabel.FontStyle = FontStyles.Italic;
    }

    // Invitation Song Event Handlers
    private void InvitationSongSelect_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Media Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma|All Files|*.*",
            Title = "Select Invitation Song"
        };

        if (dlg.ShowDialog() == true)
        {
            _invitationSongPath = dlg.FileName;
            var fileName = System.IO.Path.GetFileName(_invitationSongPath);
            _settings.InvitationSongFile = fileName;
            _settings.Save();

            InvitationSongFileLabel.Text = fileName;
            InvitationSongFileLabel.Foreground = System.Windows.Media.Brushes.Black;
            InvitationSongFileLabel.FontStyle = FontStyles.Normal;
        }
    }

    private void InvitationSongClear_Click(object sender, RoutedEventArgs e)
    {
        _invitationSongPath = null;
        _settings.InvitationSongFile = null;
        _settings.Save();

        InvitationSongFileLabel.Text = "No file selected";
        InvitationSongFileLabel.Foreground = System.Windows.Media.Brushes.Gray;
        InvitationSongFileLabel.FontStyle = FontStyles.Italic;
    }

    // Ending Song Event Handlers
    private void EndingSongSelect_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Media Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma|All Files|*.*",
            Title = "Select Ending Song"
        };

        if (dlg.ShowDialog() == true)
        {
            _endingSongPath = dlg.FileName;
            var fileName = System.IO.Path.GetFileName(_endingSongPath);
            _settings.EndingSongFile = fileName;
            _settings.LastMediaDirectory = System.IO.Path.GetDirectoryName(_endingSongPath);
            _settings.Save();

            EndingSongFileLabel.Text = fileName;
            EndingSongFileLabel.Foreground = System.Windows.Media.Brushes.Black;
            EndingSongFileLabel.FontStyle = FontStyles.Normal;
        }
    }

    private void EndingSongClear_Click(object sender, RoutedEventArgs e)
    {
        _endingSongPath = null;
        _settings.EndingSongFile = null;
        _settings.Save();

        EndingSongFileLabel.Text = "No file selected";
        EndingSongFileLabel.Foreground = System.Windows.Media.Brushes.Gray;
        EndingSongFileLabel.FontStyle = FontStyles.Italic;
    }

    private void MakeService_Click(object sender, RoutedEventArgs e)
    {
        var addedCount = 0;
        
        // Add Song for Beginning of Church if it exists
        if (!string.IsNullOrEmpty(_songForBeginningPath) && File.Exists(_songForBeginningPath))
        {
            _mainWindow.PlaylistListBox.Items.Add(new PlaylistItem(_songForBeginningPath));
            addedCount++;
        }
        
        // Add Call to Worship if it exists
        if (!string.IsNullOrEmpty(_callToWorshipPath) && File.Exists(_callToWorshipPath))
        {
            _mainWindow.PlaylistListBox.Items.Add(new PlaylistItem(_callToWorshipPath));
            addedCount++;
        }
        
        // Add Doxology if it exists
        if (!string.IsNullOrEmpty(_doxologyPath) && File.Exists(_doxologyPath))
        {
            _mainWindow.PlaylistListBox.Items.Add(new PlaylistItem(_doxologyPath));
            addedCount++;
        }
        
        // Add Praise Song if it exists
        if (!string.IsNullOrEmpty(_praiseSongPath) && File.Exists(_praiseSongPath))
        {
            _mainWindow.PlaylistListBox.Items.Add(new PlaylistItem(_praiseSongPath));
            addedCount++;
        }
        
        // Add Prayer Song if it exists
        if (!string.IsNullOrEmpty(_prayerSongPath) && File.Exists(_prayerSongPath))
        {
            _mainWindow.PlaylistListBox.Items.Add(new PlaylistItem(_prayerSongPath));
            addedCount++;
        }
        
        // Add Communion Song if it exists
        if (!string.IsNullOrEmpty(_communionSongPath) && File.Exists(_communionSongPath))
        {
            _mainWindow.PlaylistListBox.Items.Add(new PlaylistItem(_communionSongPath));
            addedCount++;
        }
        
        // Add Childrens Moment Song if it exists
        if (!string.IsNullOrEmpty(_childrensMomentSongPath) && File.Exists(_childrensMomentSongPath))
        {
            _mainWindow.PlaylistListBox.Items.Add(new PlaylistItem(_childrensMomentSongPath));
            addedCount++;
        }
        
        // Add Invitation Song if it exists
        if (!string.IsNullOrEmpty(_invitationSongPath) && File.Exists(_invitationSongPath))
        {
            _mainWindow.PlaylistListBox.Items.Add(new PlaylistItem(_invitationSongPath));
            addedCount++;
        }
        
        // Add Ending Song if it exists
        if (!string.IsNullOrEmpty(_endingSongPath) && File.Exists(_endingSongPath))
        {
            _mainWindow.PlaylistListBox.Items.Add(new PlaylistItem(_endingSongPath));
            addedCount++;
        }
        
        if (addedCount > 0)
        {
            MessageBox.Show($"Service created! Added {addedCount} elements to the playlist.", "Service Created",
                MessageBoxButton.OK, MessageBoxImage.Information);
            this.DialogResult = true;
            this.Close();
        }
        else
        {
            MessageBox.Show("No service elements selected. Please select files for at least one service element.", "No Elements Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
