using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Events
{
    public class ChannelPointRedeemEventArgs
    {
        public string UserId { get; set; } = string.Empty;
        public string Sender { get; set; } = "";
        public string Title { get; set; } = "";
        public string UserInput { get; set; } = "";
    }
}