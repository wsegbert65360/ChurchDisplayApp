using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ChurchDisplayApp.Models
{
    /// <summary>
    /// Represents a single item in the playlist with per-item volume support.
    /// Implements INotifyPropertyChanged so the UI can bind directly to Volume changes.
    /// </summary>
    public class PlaylistItem : INotifyPropertyChanged
    {
        private double _volume;

        public string FullPath { get; init; }
        public string FileName { get; init; }
        public string Extension { get; init; }

        /// <summary>
        /// Per-item playback volume (0.0 to 1.0). Default is 0.8 (80%).
        /// </summary>
        public double Volume
        {
            get => _volume;
            set
            {
                var clamped = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(_volume - clamped) > 0.001)
                {
                    _volume = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public PlaylistItem(string fullPath, double volume = 0.8)
        {
            FullPath = fullPath;
            FileName = Path.GetFileName(fullPath);
            Extension = Path.GetExtension(fullPath).ToLower();
            Volume = volume;
        }

        public override string ToString()
        {
            return FileName;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
