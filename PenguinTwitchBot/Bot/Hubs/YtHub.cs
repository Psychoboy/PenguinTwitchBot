using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PenguinTwitchBot.Bot.Commands.Music
{
    public class YtHub(YtPlayer ytPlayer) : Hub
    {
        private readonly YtPlayer _ytPlayer = ytPlayer ?? throw new ArgumentNullException("ytplayer");

        /// <summary>
        /// Sends current song request state when a client connects.
        /// This confirms the connection and provides initial data without waiting for the next broadcast.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            var currentRequests = await _ytPlayer.GetRequestedSongs();
            await Clients.Caller.SendAsync("CurrentSongRequests", currentRequests);
        }

        [Authorize(Roles = "Streamer")]
        public async Task PlayNextVideo()
        {
            await _ytPlayer.PlayNextSong();
        }

        [Authorize(Roles = "Streamer")]
        public async Task UpdateState(int state)
        {
            await _ytPlayer.UpdateState(state);
        }

        [Authorize(Roles = "Streamer")]
        public async Task LoadNextSong()
        {
            await _ytPlayer.PlayNextSong();
        }

        [Authorize(Roles = "Streamer")]
        public async Task SongError(object errorCode)
        {
            await _ytPlayer.SongError(errorCode);
        }
    }
}