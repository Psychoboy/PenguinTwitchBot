using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingGameplayService
    {
        Task<FishingAttemptResult> PerformFishingAttempt(string userId, string username);

        Task<FishingAttemptResult> PerformFishingAttempt(string userId, string username, bool deferRewards)
            => PerformFishingAttempt(userId, username);

        Task<List<FishingPointRewardAwarded>> AwardDeferredCatchRewardsAsync(
            Guid catchId,
            string userId,
            string username,
            int gold,
            IReadOnlyList<FishingPointRewardAwarded> plannedPointRewards)
            => Task.FromResult(plannedPointRewards.ToList());
    }
}
