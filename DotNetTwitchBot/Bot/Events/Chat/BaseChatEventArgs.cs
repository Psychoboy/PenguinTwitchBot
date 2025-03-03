using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Events.Chat
{
    public class BaseChatEventArgs
    {
        public bool IsSub { get; set; }
        public bool IsMod { get; set; }
        public bool IsVip { get; set; }
        public bool IsBroadcaster { get; set; }
        public string DisplayName { get; set; } = "";
        public string Name { get; set; } = "";
        public string UserId { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;

        public bool IsSubOrHigher()
        {
            return IsSub || IsMod || IsVip || IsBroadcaster;
        }

        public bool IsVipOrHigher()
        {
            return IsVip || IsMod || IsBroadcaster;
        }

        public bool IsModOrHigher()
        {
            return IsMod || IsBroadcaster;
        }
    }
}