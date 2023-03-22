using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace DotNetTwitchBot.Hubs
{
    public class YtHub : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        public async Task PlayNextVideo()
        {
            await Clients.All.SendAsync("PlayVideo", "4aVZanf6NR0");
        }
    }
}