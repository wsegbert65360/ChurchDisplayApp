using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System;
using System.IO;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp.Services
{
    public class PlaylistDragDropManager
    {
        private readonly ListBox _playlistListBox;
        private readonly PlaylistManager _playlistManager;
        private readonly Action _onPlaylistChanged;
        private Point _dragStartPoint;

        public PlaylistDragDropManager(ListBox playlistListBox, PlaylistManager playlistManager, Action onPlaylistChanged)
        {
            _playlistListBox = playlistListBox;
            _playlistManager = playlistManager;
            _onPlaylistChanged = onPlaylistChanged;
        }

        public void HandlePlaylistDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                _playlistManager.AddFiles(files);
                _onPlaylistChanged?.Invoke();
            }
        }

        public void HandleListBoxDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("PlaylistItem"))
            {
                e.Effects = DragDropEffects.Move;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        public void HandleListBoxPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        public void HandleListBoxPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                var position = e.GetPosition(null);
                if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (_playlistListBox.SelectedItem is PlaylistItem item)
                    {
                        var dataObject = new DataObject();
                        dataObject.SetData("PlaylistItem", item);
                        dataObject.SetData(DataFormats.FileDrop, new[] { item.FullPath });
                        DragDrop.DoDragDrop(_playlistListBox, dataObject, DragDropEffects.Move);
                    }
                }
            }
        }

        public void HandleListBoxDrop(object sender, DragEventArgs e)
        {
            try
            {
                int dropIndex = GetDropIndex(e);

                if (e.Data.GetDataPresent("PlaylistItem"))
                {
                    var draggedItem = e.Data.GetData("PlaylistItem") as PlaylistItem;
                    if (draggedItem != null)
                    {
                        int oldIndex = _playlistManager.Items.IndexOf(draggedItem);
                        if (oldIndex != -1 && oldIndex != dropIndex)
                        {
                            _playlistManager.Items.Move(oldIndex, dropIndex > oldIndex ? dropIndex - 1 : dropIndex);
                            _playlistListBox.SelectedIndex = _playlistManager.Items.IndexOf(draggedItem);
                            _onPlaylistChanged?.Invoke();
                        }
                    }
                }
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        int insertIndex = dropIndex;
                        foreach (var file in files)
                        {
                            if (_playlistManager.ValidateFile(file))
                            {
                                var item = new PlaylistItem(file);
                                _playlistManager.Items.Insert(insertIndex, item);
                                insertIndex++;
                            }
                        }
                        _playlistListBox.SelectedIndex = Math.Min(insertIndex - 1, _playlistManager.Items.Count - 1);
                        _onPlaylistChanged?.Invoke();
                    }
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during drop operation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int GetDropIndex(DragEventArgs e)
        {
            var position = e.GetPosition(_playlistListBox);
            for (int i = 0; i < _playlistListBox.Items.Count; i++)
            {
                var listBoxItem = _playlistListBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (listBoxItem != null)
                {
                    var itemPosition = listBoxItem.TranslatePoint(new Point(0, 0), _playlistListBox);
                    var itemHeight = listBoxItem.ActualHeight;
                    if (position.Y < itemPosition.Y + itemHeight)
                    {
                        if (position.Y < itemPosition.Y + itemHeight / 2)
                            return i;
                        else
                            return i + 1;
                    }
                }
            }
            return _playlistManager.Items.Count;
        }
    }
}
