namespace DotNetTwitchBot.Bot.Models.Fishing
{
    public class SnapLossLeaderboardPosition
    {
        public int Rank { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal TotalGoldLost { get; set; }
        public int TotalItemsLost { get; set; }
        public int SnapCount { get; set; }
    }
}
