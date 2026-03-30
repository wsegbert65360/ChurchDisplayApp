using System;
using System.IO;

namespace ChurchDisplayApp.Models
{
    public class PlaylistItem
    {
        public string FullPath { get; init; }
        public string FileName { get; init; }
        public string Extension { get; init; }

        public PlaylistItem(string fullPath)
        {
            FullPath = fullPath;
            FileName = Path.GetFileName(fullPath);
            Extension = Path.GetExtension(fullPath).ToLower();
        }

        public override string ToString()
        {
            return FileName;
        }
    }
}
