using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models.Duel
{
    public class PendingDuel
    {
        public string Attacker { get; set; } = null!;
        public string Defender { get; set; } = null!;
        public long Amount { get; set; }
        public DateTime ExpiresAt { get; set; } = DateTime.Now.AddMinutes(2);
        public PlatformType Platform { get; set; }
    }
}