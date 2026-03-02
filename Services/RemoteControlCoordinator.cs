using System;
using System.Collections.Generic;
using ChurchDisplayApp.ViewModels;
using ChurchDisplayApp.Interfaces;

namespace ChurchDisplayApp.Services
{
    public sealed record RemotePlaylistItem(int Index, string FileName, string FullPath);
    public record RemoteStatus(string Title, double Progress, string CurrentTime, string Duration, double Volume);

    public class RemoteControlCoordinator : IDisplayController
    {
        private readonly IDisplayController _controller;

        public RemoteControlCoordinator(IDisplayController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        public List<RemotePlaylistItem> GetPlaylistItems() => _controller.GetPlaylistItems();
        public void PlayIndex(int index) => _controller.PlayIndex(index);
        public void Stop() => _controller.Stop();
        public void Blank() => _controller.Blank();
        public void SetVolume(double volume) => _controller.SetVolume(volume);
        public RemoteStatus GetStatus() => _controller.GetStatus();

        // Convenience methods for internal use if needed
        public void Next() => _controller.Next();
        public void Previous() => _controller.Previous();
    }
}
