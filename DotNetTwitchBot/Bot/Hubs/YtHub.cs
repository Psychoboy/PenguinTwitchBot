using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace DotNetTwitchBot.Bot.Commands.Music
{
    public class YtHub : Hub
    {
        private readonly YtPlayer _ytPlayer;

        public YtHub(YtPlayer ytPlayer)
        {
            _ytPlayer = ytPlayer ?? throw new ArgumentNullException("ytplayer");
        }

        public async Task PlayNextVideo()
        {
            await _ytPlayer.PlayNextSong();
        }

        public void UpdateState(int state)
        {
            _ytPlayer.UpdateState(state);
        }

        public async Task LoadNextSong()
        {
            await _ytPlayer.LoadNextSong();
        }

        public async Task SongError(object errorCode)
        {
            await _ytPlayer.SongError(errorCode);
        }
    }
}