using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Events
{
    public class FollowEventArgs
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTime FollowDate { get; set; } = DateTime.MinValue;
        public PlatformType Platform { get; set; } = PlatformType.Twitch;
    }
}