using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Misc;
using Microsoft.AspNetCore.SignalR;

namespace DotNetTwitchBot.Hubs
{
    public class YtHub : Hub
    {
        private readonly IYtPlayer _ytPlayer;

        public YtHub(IYtPlayer ytPlayer)
        {
            if (ytPlayer == null)
            {
                throw new ArgumentNullException("ytplayer");
            }
            _ytPlayer = ytPlayer;
        }

        private async Task SkipSong(object? sender, string e)
        {
            await Clients.All.SendAsync("PlayVideo", e);
        }

        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        public async Task PlayNextVideo()
        {
            await Clients.All.SendAsync("PlayVideo", _ytPlayer.GetNextSong());
        }
    }
}