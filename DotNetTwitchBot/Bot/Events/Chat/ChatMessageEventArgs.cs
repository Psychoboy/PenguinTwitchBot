using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Events.Chat
{
    public class ChatMessageEventArgs : BaseChatEventArgs
    {
        public string Message { get; set; } = "";
        public bool FromOwnChannel { get; internal set; }
    }
}