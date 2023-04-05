using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Events
{
    public class RaidEventArgs
    {
        public string Name { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public int NumberOfViewers { get; set; }
    }
}