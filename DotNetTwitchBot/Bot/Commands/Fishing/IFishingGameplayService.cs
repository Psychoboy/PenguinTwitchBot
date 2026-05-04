using DotNetTwitchBot.Bot.Models.Fishing;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingGameplayService
    {
        Task<FishingAttemptResult> PerformFishingAttempt(string userId, string username);
    }
}
