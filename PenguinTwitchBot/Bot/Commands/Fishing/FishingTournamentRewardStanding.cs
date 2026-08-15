namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    /// <summary>
    /// The player holding a reward rule's placement, using that rule's own score category
    /// rather than the tournament's primary one.
    /// </summary>
    public sealed class FishingTournamentRewardStanding
    {
        public int RewardRuleId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public double Score { get; set; }
        public int CatchCount { get; set; }
    }
}
