using PenguinTwitchBot.Database.Bot.Models;

namespace PenguinTwitchBot.Bot.Commands.Misc
{
    public class RaidHistoryItem
    {
        public RaidHistoryEntry? HistoryEntry { get; init; }
        public string UserId { get; init; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int TotalIncomingRaids { get; init; }
        public int TotalIncomingRaidViewers { get; init; }
        public int TotalOutgoingRaids { get; init; }
        public int TotalOutGoingRaidViewers { get; init; }
        public bool IsOnline { get; set; }
        public DateTime LastIncomingRaid { get; init; } = DateTime.MinValue;
        public DateTime LastOutgoingRaid { get; init; } = DateTime.MinValue;
        public string LastGame { get; set; } = string.Empty;
        public bool IsStreamTeamMember { get; set; }
        public IReadOnlyList<string> StreamTeamNames { get; set; } = [];
        public bool IsHistoryEntry => HistoryEntry != null;
    }
}
