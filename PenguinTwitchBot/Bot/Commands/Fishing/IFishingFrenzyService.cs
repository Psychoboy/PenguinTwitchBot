namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public sealed class FishingFrenzyState
    {
        public string Name { get; init; } = "Fishing Frenzy";
        public double RarityMultiplier { get; init; } = 2.0;
        public string? TargetCategory { get; init; }
        public double CategoryBoost { get; init; } = 0.0;
        public DateTime StartedAtUtc { get; init; }
        public DateTime EndsAtUtc { get; init; }

        public bool IsActiveAt(DateTime utcNow) => utcNow < EndsAtUtc;
        public TimeSpan GetRemaining(DateTime utcNow) => EndsAtUtc > utcNow ? EndsAtUtc - utcNow : TimeSpan.Zero;
    }

    public interface IFishingFrenzyService
    {
        FishingFrenzyState? GetActiveFrenzy();
        Task<FishingFrenzyState> StartFrenzyAsync(
            string name,
            int durationMinutes,
            double rarityMultiplier = 2.0,
            string? targetCategory = null,
            double categoryBoost = 0.5,
            bool announceInChat = true);
        Task<bool> StopFrenzyAsync(bool announceInChat = true);
    }
}
