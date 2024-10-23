using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Events
{
    public class SubscriptionEndEventArgs
    {
        public string? Name { get; set; }
        public string UserId { get; set; } = string.Empty;
    }
}