using System.ComponentModel;
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
        private bool _isMissing;

        /// <summary>Gets the full path of the media file.</summary>
        public string FullPath { get; init; }

        /// <summary>Gets the file name (without folder).</summary>
        public string FileName { get; init; }

        /// <summary>Gets the lower-case file extension including the dot.</summary>
        public string Extension { get; init; }

        /// <summary>
        /// Gets or sets whether the file was not found the last time it was checked
        /// (for example, the USB drive is unplugged). The item stays in the playlist so it is not lost on save.
        /// </summary>
        public bool IsMissing
        {
            get => _isMissing;
            set
            {
                if (_isMissing != value)
                {
                    _isMissing = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Per-item playback volume (0.0 to 1.0). Default is 1.0 (100%).
        /// </summary>
        public double Volume
        {
            get => _volume;
            set
            {
                // NaN would be saved as invalid JSON and make the playlist unsaveable.
                if (!double.IsFinite(value))
                    return;

                var clamped = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(_volume - clamped) > 0.001)
                {
                    _volume = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public PlaylistItem(string fullPath, double volume = 1.0)
        {
            FullPath = fullPath;
            FileName = Path.GetFileName(fullPath);
            Extension = Path.GetExtension(fullPath).ToLowerInvariant();
            Volume = double.IsFinite(volume) ? volume : 1.0;
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
