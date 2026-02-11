using System;
using System.Collections.Generic;
using ChurchDisplayApp.ViewModels;

namespace ChurchDisplayApp.Services
{
    public sealed record RemotePlaylistItem(int Index, string FileName, string FullPath);
    public record RemoteStatus(string Title, double Progress, string CurrentTime, string Duration, double Volume);

    public class RemoteControlCoordinator
    {
        private readonly PlaylistManager _playlistManager;
        private readonly MediaControlService _mediaControlService;
        private readonly MainViewModel _viewModel;

        public RemoteControlCoordinator(
            PlaylistManager playlistManager, 
            MediaControlService mediaControlService, 
            MainViewModel viewModel)
        {
            _playlistManager = playlistManager;
            _mediaControlService = mediaControlService;
            _viewModel = viewModel;
        }

        public List<RemotePlaylistItem> GetPlaylistItems()
        {
            var result = new List<RemotePlaylistItem>();
            for (int i = 0; i < _playlistManager.Items.Count; i++)
            {
                var playlistItem = _playlistManager.Items[i];
                result.Add(new RemotePlaylistItem(i, playlistItem.FileName, playlistItem.FullPath));
            }
            return result;
        }

        public void PlayIndex(int index)
        {
            if (index >= 0 && index < _playlistManager.Items.Count)
            {
                _viewModel.SelectedItem = _playlistManager.Items[index];
                _viewModel.PlayCommand.Execute(null);
            }
        }

        public void Stop() => _viewModel.StopCommand.Execute(null);
        public void Blank() => _viewModel.BlankCommand.Execute(null);
        public void SetVolume(double volume) => _viewModel.Volume = volume;

        public RemoteStatus GetStatus()
        {
            var progress = _mediaControlService.GetProgress();
            return new RemoteStatus(
                _viewModel.CurrentMediaTitle,
                progress.ProgressPercent,
                _viewModel.FormatTime(progress.CurrentTime),
                _viewModel.FormatTime(progress.Duration),
                _viewModel.Volume
            );
        }
    }
}
