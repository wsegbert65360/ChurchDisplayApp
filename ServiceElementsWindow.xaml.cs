using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ChurchDisplayApp.Models;
using ChurchDisplayApp.Services;

namespace ChurchDisplayApp;

public partial class ServiceElementsWindow : Window
{
    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly AppSettings _settings = AppSettings.Current;
    private readonly PlaylistManager _playlistManager;
    private readonly ServicePlanService _servicePlanService = new();

    // Tracks the per-slot UI row containers keyed by slot Id, so we can
    // refresh a single row without rebuilding the whole list.
    private readonly Dictionary<string, Grid> _slotRows = new();

    // ── Constructor ───────────────────────────────────────────────────────────
    public ServiceElementsWindow(PlaylistManager playlistManager)
    {
        InitializeComponent();
        _playlistManager = playlistManager
            ?? throw new ArgumentNullException(nameof(playlistManager));

        ValidateStickyFiles();  // Check stored paths before building UI
        BuildSlotList();
    }

    // ── Sticky file validation ─────────────────────────────────────────────────
    /// <summary>
    /// For every slot: if IsSticky=false, clear the path.
    /// If IsSticky=true but the file no longer exists, show red (handled in BuildSlotRow)
    /// and clear the path so it won't be used.
    /// </summary>
    private void ValidateStickyFiles()
    {
        bool changed = false;

        foreach (var slot in _settings.ServiceSlots)
        {
            if (!slot.IsSticky)
            {
                // Non-sticky slots always start fresh
                if (!string.IsNullOrEmpty(slot.FilePath))
                {
                    slot.FilePath = null;
                    changed = true;
                }
            }
            else if (!string.IsNullOrEmpty(slot.FilePath) && !File.Exists(slot.FilePath))
            {
                // Sticky but file is gone — clear it (UI will show red briefly then rebuild)
                slot.FilePath = null;
                changed = true;
                Serilog.Log.Warning("Sticky file for slot '{Slot}' not found — cleared.", slot.DisplayName);
            }
        }

        if (changed)
            _settings.Save();
    }

    // ── Slot list UI ───────────────────────────────────────────────────────────
    /// <summary>
    /// Rebuilds the entire slot list in the ItemsControl.
    /// Call this after adding/removing/reordering slots.
    /// </summary>
    private void BuildSlotList()
    {
        _slotRows.Clear();
        var panel = new StackPanel();

        foreach (var slot in _settings.ServiceSlots)
        {
            var row = BuildSlotRow(slot);
            _slotRows[slot.Id] = row;
            panel.Children.Add(row);

            // Separator line
            panel.Children.Add(new Separator { Margin = new Thickness(0, 2, 0, 2) });
        }

        // Use Content since we're bypassing ItemTemplate
        SlotListControl.Content = panel;
    }

    /// <summary>
    /// Builds a single slot row Grid.
    /// Layout: [Name label] [File label] [Select] [Clear] [Add to playlist] [Remember checkbox]
    /// </summary>
    private Grid BuildSlotRow(ServiceSlot slot)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3), Tag = slot.Id };

        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

        // Col 0: Slot display name
        var nameLabel = new TextBlock
        {
            Text = slot.DisplayName,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = slot.DisplayName,
            Margin = new Thickness(4, 0, 8, 0),
            FontWeight = FontWeights.SemiBold
        };
        Grid.SetColumn(nameLabel, 0);

        // Col 1: File label — shows filename, "No file selected", or missing indicator
        var fileLabel = BuildFileLabel(slot);
        Grid.SetColumn(fileLabel, 1);

        // Col 2: Select button
        var selectBtn = new Button
        {
            Content = "Select…",
            Padding = new Thickness(8, 4, 8, 4),
            MinWidth = 0,
            Margin = new Thickness(2, 0, 2, 0),
            Tag = slot.Id
        };
        selectBtn.Click += SelectFile_Click;
        Grid.SetColumn(selectBtn, 2);

        // Col 3: Clear button
        var clearBtn = new Button
        {
            Content = "Clear",
            Padding = new Thickness(8, 4, 8, 4),
            MinWidth = 0,
            Margin = new Thickness(2, 0, 2, 0),
            Tag = slot.Id
        };
        clearBtn.Click += ClearSlot_Click;
        Grid.SetColumn(clearBtn, 3);

        // Col 4: Add (Use) button
        var useBtn = new Button
        {
            Content = "Add",
            Padding = new Thickness(8, 4, 8, 4),
            MinWidth = 0,
            Margin = new Thickness(2, 0, 2, 0),
            Tag = slot.Id,
            IsEnabled = !string.IsNullOrEmpty(slot.FilePath) && File.Exists(slot.FilePath),
            ToolTip = "Add this file to the playlist"
        };
        useBtn.Click += UseSlot_Click;
        Grid.SetColumn(useBtn, 4);

        // Col 5: Remember checkbox
        var rememberCheck = new CheckBox
        {
            IsChecked = slot.IsSticky,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Remember this file between services",
            Tag = slot.Id
        };
        rememberCheck.Checked   += RememberCheck_Changed;
        rememberCheck.Unchecked += RememberCheck_Changed;
        Grid.SetColumn(rememberCheck, 5);

        row.Children.Add(nameLabel);
        row.Children.Add(fileLabel);
        row.Children.Add(selectBtn);
        row.Children.Add(clearBtn);
        row.Children.Add(useBtn);
        row.Children.Add(rememberCheck);

        return row;
    }

    /// <summary>
    /// Creates the file label TextBlock with appropriate style for the slot's current state.
    /// </summary>
    private TextBlock BuildFileLabel(ServiceSlot slot)
    {
        var label = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(4, 0, 4, 0),
            Tag = slot.Id + "_label"
        };

        ApplyFileLabelState(label, slot);
        return label;
    }

    /// <summary>
    /// Updates the visual state of a file label based on whether a file is assigned and exists.
    /// </summary>
    private static void ApplyFileLabelState(TextBlock label, ServiceSlot slot)
    {
        if (string.IsNullOrEmpty(slot.FilePath))
        {
            label.Text       = "No file selected";
            label.Foreground = Brushes.Gray;
            label.FontStyle  = FontStyles.Italic;
            label.ToolTip    = null;
        }
        else if (!File.Exists(slot.FilePath))
        {
            label.Text       = "⚠ File not found — cleared";
            label.Foreground = Brushes.Red;
            label.FontStyle  = FontStyles.Italic;
            label.ToolTip    = slot.FilePath;
        }
        else
        {
            label.Text       = Path.GetFileName(slot.FilePath);
            label.Foreground = Brushes.DarkGreen;
            label.FontStyle  = FontStyles.Normal;
            label.ToolTip    = slot.FilePath;
        }
    }

    /// <summary>
    /// Refreshes just the file label and Add button in an existing row without rebuilding everything.
    /// </summary>
    private void RefreshSlotRow(ServiceSlot slot)
    {
        if (!_slotRows.TryGetValue(slot.Id, out var row)) return;

        foreach (var child in row.Children)
        {
            if (child is TextBlock tb && tb.Tag is string tag && tag == slot.Id + "_label")
                ApplyFileLabelState(tb, slot);

            if (child is Button btn && btn.Tag is string btnTag && btnTag == slot.Id && btn.Content?.ToString() == "Add")
                btn.IsEnabled = !string.IsNullOrEmpty(slot.FilePath) && File.Exists(slot.FilePath);
        }
    }

    // ── Slot row event handlers ────────────────────────────────────────────────
    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;
        var slot = GetSlot(id);
        if (slot == null) return;

        var dlg = new OpenFileDialog
        {
            Filter = "Media Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma|All Files|*.*",
            Title  = $"Select: {slot.DisplayName}"
        };

        // Open in this slot's last-used folder, falling back to global last directory
        var startDir = slot.LastUsedFolder ?? _settings.LastMediaDirectory;
        if (!string.IsNullOrEmpty(startDir) && Directory.Exists(startDir))
            dlg.InitialDirectory = startDir;

        if (dlg.ShowDialog() == true)
        {
            slot.FilePath       = dlg.FileName;
            slot.LastUsedFolder = Path.GetDirectoryName(dlg.FileName);
            _settings.LastMediaDirectory = slot.LastUsedFolder; // Also update global fallback
            _settings.Save();

            RefreshSlotRow(slot);
        }
    }

    private void ClearSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;
        var slot = GetSlot(id);
        if (slot == null) return;

        slot.FilePath = null;
        _settings.Save();
        RefreshSlotRow(slot);
    }

    private void UseSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;
        var slot = GetSlot(id);
        if (slot == null) return;

        if (string.IsNullOrEmpty(slot.FilePath) || !File.Exists(slot.FilePath))
        {
            MessageBox.Show($"No valid file assigned to '{slot.DisplayName}'.",
                "No File", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _playlistManager.AddFiles(new[] { slot.FilePath });
        MessageBox.Show($"'{slot.DisplayName}' added to playlist.",
            "Added", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RememberCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not string id) return;
        var slot = GetSlot(id);
        if (slot == null) return;

        slot.IsSticky = cb.IsChecked == true;
        _settings.Save();
    }

    // ── Bottom bar button handlers ─────────────────────────────────────────────
    private void MakeService_Click(object sender, RoutedEventArgs e)
    {
        var validSlots = _settings.ServiceSlots
            .Where(s => !string.IsNullOrEmpty(s.FilePath) && File.Exists(s.FilePath))
            .ToList();

        if (validSlots.Count == 0)
        {
            MessageBox.Show("No slots have valid files assigned. Please select files first.",
                "Nothing to Add", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        foreach (var slot in validSlots)
            _playlistManager.AddFiles(new[] { slot.FilePath! });

        MessageBox.Show($"Service created! Added {validSlots.Count} items to the playlist.",
            "Service Created", MessageBoxButton.OK, MessageBoxImage.Information);

        DialogResult = true;
        Close();
    }



    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // ── Settings panel ─────────────────────────────────────────────────────────
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new ServiceElementsSettingsWindow(_settings)
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() == true)
        {
            _settings.Save();
            BuildSlotList(); // Rebuild in case slots were added/removed/renamed
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private ServiceSlot? GetSlot(string id) =>
        _settings.ServiceSlots.FirstOrDefault(s => s.Id == id);
}
