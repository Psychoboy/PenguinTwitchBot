namespace DotNetTwitchBot.Bot.Models
{
    public class LurkBaitTrigger
    {
        public string Trigger { get; set; } = "";
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string CastTrigger { get; set; } = "";
        public string? CatchName { get; set; }
        public string? CatchDescription { get; set; }
        public string? CatchRarity { get; set; }
        public bool IsCustomCatch { get; set; }
        public int CatchRating { get; set; }
        public int CatchValue { get; set; }
        public string? CatchWeight { get; set; }
        public string? CatchThumbnailURL { get; set; }
        public int PlayerGold { get; set; }
        public int PlayerLifetimeGold { get; set; }
        public int PlayerLeaderboardGold { get; set; }
    }
}
