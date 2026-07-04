namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public sealed class FishingTournamentStanding
    {
        public int Rank { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public double Score { get; set; }
        public int CatchCount { get; set; }
        public DateTime? LastCaughtAtUtc { get; set; }
    }
}