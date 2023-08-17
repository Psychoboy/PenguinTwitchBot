using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Events
{
    public class CheerEventArgs
    {
        public string? Name { get; set; }
        public string DisplayName { get; set; } = null!;
        public string Message { get; set; } = "";
        public int Amount { get; set; }

        public bool IsAnonymous { get; set; }

    }
}