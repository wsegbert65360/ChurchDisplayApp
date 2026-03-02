using ChurchDisplayApp.Services;

namespace ChurchDisplayApp.Interfaces
{
    /// <summary>
    /// Interface for controlling display operations from external services like the Remote Control Server.
    /// </summary>
    public interface IDisplayController
    {
        /// <summary>Moves to the next item in the playlist.</summary>
        void Next();
        
        /// <summary>Moves to the previous item in the playlist.</summary>
        void Previous();
        
        /// <summary>Stops media playback.</summary>
        void Stop();
        
        /// <summary>Blanks the output display.</summary>
        void Blank();
        
        /// <summary>Sets the application volume (0.0 to 1.0).</summary>
        void SetVolume(double volume);
        
        /// <summary>Plays a specific item in the playlist by index.</summary>
        void PlayIndex(int index);
        
        /// <summary>Gets the current status of the display and playback.</summary>
        RemoteStatus GetStatus();
        
        /// <summary>Gets a list of items currently in the playlist for the remote UI.</summary>
        List<RemotePlaylistItem> GetPlaylistItems();
    }
}
