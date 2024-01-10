using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DotNetTwitchBot.Bot.Commands.Music
{
    public class YtHub(YtPlayer ytPlayer) : Hub
    {
        private readonly YtPlayer _ytPlayer = ytPlayer ?? throw new ArgumentNullException("ytplayer");

        [Authorize(Roles = "Streamer")]
        public async Task PlayNextVideo()
        {
            await _ytPlayer.PlayNextSong();
        }

        [Authorize(Roles = "Streamer")]
        public void UpdateState(int state)
        {
            _ytPlayer.UpdateState(state);
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