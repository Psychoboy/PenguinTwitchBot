using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Events.Chat
{
    public class BaseChatEventArgs
    {
        public bool isSub { get; set; }
        public bool isMod { get; set; }
        public bool isVip { get; set; }
        public bool isBroadcaster { get; set; }
        public string DisplayName { get; set; } = "";
        public string Name { get; set; } = "";

        public bool IsSubOrHigher()
        {
            return isSub || isMod || isVip || isBroadcaster;
        }

        public bool IsVipOrHigher()
        {
            return isVip || isMod || isBroadcaster;
        }

        public bool IsModOrHigher()
        {
            return isMod || isBroadcaster;
        }
    }
}