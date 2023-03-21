using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Events
{
    public class ChatMessageEventArgs
    {
        public string Message { get; set; } = "";
        public string Sender { get; set; } = "";

        public bool isSub { get; set; }
        public bool isMod { get; set; }
        public bool isVip { get; set; }
        public bool isBroadcaster { get; set; }
        public string DisplayName { get; set; } = "";
    }
}