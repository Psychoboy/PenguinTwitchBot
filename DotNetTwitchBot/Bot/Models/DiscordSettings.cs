using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class DiscordSettings
    {
        public ulong BroadcastChannel { get; set; }
        public ulong PingRoleWhenLive { get; set; }
        public ulong RoleIdToAssignMemberWhenLive { get; set; }
        public ulong DiscordServerId { get; set; }
        public string? DiscordToken { get; set; }
    }
}