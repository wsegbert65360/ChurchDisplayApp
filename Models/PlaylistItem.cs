using System;
using System.IO;

namespace ChurchDisplayApp.Models
{
    public class PlaylistItem
    {
        public string FullPath { get; set; }
        public string FileName { get; set; }
        public string Extension { get; set; }

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
