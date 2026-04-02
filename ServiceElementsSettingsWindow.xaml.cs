using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp;

public partial class ServiceElementsSettingsWindow : Window
{
    private readonly AppSettings _settings;

    // Working copy of slots — we only commit to _settings on OK
    private readonly ObservableCollection<ServiceSlot> _workingSlots;

    public ServiceElementsSettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        // Deep-copy the slots so Cancel truly cancels
        _workingSlots = new ObservableCollection<ServiceSlot>(
            settings.ServiceSlots.Select(s => new ServiceSlot
            {
                Id             = s.Id,
                DisplayName    = s.DisplayName,
                FilePath       = s.FilePath,
                IsSticky       = s.IsSticky,
                LastUsedFolder = s.LastUsedFolder
            }));

        SlotListBox.ItemsSource = _workingSlots;
        DefaultFolderBox.Text   = settings.LastMediaDirectory ?? string.Empty;
    }

    // ── OK / Cancel ────────────────────────────────────────────────────────────
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        // Commit working slots back to settings
        _settings.ServiceSlots.Clear();
        foreach (var slot in _workingSlots)
            _settings.ServiceSlots.Add(slot);

        var folder = DefaultFolderBox.Text.Trim();
        _settings.LastMediaDirectory = string.IsNullOrEmpty(folder) ? null : folder;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // ── Slot management ────────────────────────────────────────────────────────
    private void AddSlot_Click(object sender, RoutedEventArgs e)
    {
        var newSlot = new ServiceSlot { DisplayName = "New Slot" };
        _workingSlots.Add(newSlot);
        SlotListBox.SelectedItem = newSlot;
        SlotListBox.ScrollIntoView(newSlot);
        StartRenaming(newSlot);
    }

    private void RemoveSlot_Click(object sender, RoutedEventArgs e)
    {
        if (SlotListBox.SelectedItem is not ServiceSlot slot) return;

        var result = MessageBox.Show(
            $"Remove '{slot.DisplayName}'? This will also clear any file assigned to it.",
            "Remove Slot", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            _workingSlots.Remove(slot);
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var index = SlotListBox.SelectedIndex;
        if (index <= 0) return;
        _workingSlots.Move(index, index - 1);
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var index = SlotListBox.SelectedIndex;
        if (index < 0 || index >= _workingSlots.Count - 1) return;
        _workingSlots.Move(index, index + 1);
    }

    private void SlotListBox_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SlotListBox.SelectedItem is ServiceSlot slot)
            StartRenaming(slot);
    }

    // ── Inline rename ──────────────────────────────────────────────────────────
    private void StartRenaming(ServiceSlot slot)
    {
        var dialog = new Window
        {
            Title                 = "Rename Slot",
            Width                 = 320,
            Height                = 130,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner                 = this,
            ResizeMode            = ResizeMode.NoResize
        };

        var stack = new StackPanel { Margin = new Thickness(12) };
        stack.Children.Add(new TextBlock { Text = "Enter new slot name:", Margin = new Thickness(0, 0, 0, 6) });

        var textBox = new TextBox { Text = slot.DisplayName, Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(textBox);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var okBtn     = new Button { Content = "OK",     Width = 70, Margin = new Thickness(4, 0, 0, 0), IsDefault = true };
        var cancelBtn = new Button { Content = "Cancel", Width = 70, Margin = new Thickness(4, 0, 0, 0), IsCancel  = true };

        okBtn.Click     += (s, e) => { dialog.DialogResult = true;  dialog.Close(); };
        cancelBtn.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };

        btnRow.Children.Add(okBtn);
        btnRow.Children.Add(cancelBtn);
        stack.Children.Add(btnRow);
        dialog.Content = stack;

        textBox.SelectAll();
        textBox.Focus();

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            var newName = textBox.Text.Trim();
            if (newName == slot.DisplayName) return;

            slot.DisplayName = newName;
            var index = _workingSlots.IndexOf(slot);
            _workingSlots.RemoveAt(index);
            _workingSlots.Insert(index, slot);
            SlotListBox.SelectedIndex = index;
        }
    }

    // ── Default folder ─────────────────────────────────────────────────────────
    private void BrowseDefaultFolder_Click(object sender, RoutedEventArgs e)
    {
        // Use OpenFileDialog trick to pick a folder (FolderBrowserDialog requires WinForms)
        var dlg = new OpenFileDialog
        {
            Title            = "Select Default Media Folder",
            CheckFileExists  = false,
            FileName         = "Select Folder",
            Filter           = "Folders|*.none",
            ValidateNames    = false,
            CheckPathExists  = true
        };

        var current = DefaultFolderBox.Text.Trim();
        if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
            dlg.InitialDirectory = current;

        if (dlg.ShowDialog() == true)
        {
            DefaultFolderBox.Text = Path.GetDirectoryName(dlg.FileName) ?? dlg.FileName;
        }
    }
}
