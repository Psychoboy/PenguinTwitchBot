using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static DotNetTwitchBot.Bot.Core.ServiceBackbone;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public interface IYtPlayer
    {
        public string GetNextSong();
    }
}