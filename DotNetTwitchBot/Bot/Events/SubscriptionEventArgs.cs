using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Events
{
    public class SubscriptionEventArgs
    {
        public string Name { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public int? Count { get; set; } = null;
        public bool IsGift { get; set; } = false;
        public bool IsRenewal { get; set; } = false;
    }
}