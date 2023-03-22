using System.Runtime.InteropServices.ComTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using Microsoft.AspNetCore.SignalR;
using DotNetTwitchBot.Hubs;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class YtPlayer : BaseCommand, IYtPlayer
    {
        private IHubContext<YtHub> _hubContext;

        //public HubConnectionContext Clients { get; }
        public YtPlayer(
            //HubConnectionContext clients,
            IHubContext<Hubs.YtHub> hubContext,
            ServiceBackbone serviceBackbone
        ) : base(serviceBackbone)
        {
            // if (clients == null)
            // {
            //     throw new ArgumentNullException("clients");
            // }
            // Clients = clients;
            _hubContext = hubContext;
        }

        public string GetNextSong()
        {
            return "4aVZanf6NR0";
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            //https://beta.decapi.me/youtube/videoid?search=
            if (e.Command.Equals("testnextsong"))
            {
                await _hubContext.Clients.All.SendAsync("PlayVideo", "x-64CaD8GXw");
            }
        }
    }
}