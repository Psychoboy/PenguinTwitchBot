using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class RaidHistoryEntry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string UserId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public int TotalIncomingRaids { get; set; }
        public int TotalIncomingRaidViewers { get; set; }
        public int TotalOutgoingRaids { get; set; }
        public int TotalOutGoingRaidViewers { get; set; }
        public bool IsOnline { get; set; } = false;
        public DateTime LastIncomingRaid { get; set; } = DateTime.Now;
        public DateTime LastOutgoingRaid { get; set; } = DateTime.Now;
        public DateTime LastCheckOnline { get; set; } = DateTime.MinValue;
    }
}