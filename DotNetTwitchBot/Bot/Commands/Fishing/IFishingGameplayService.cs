using DotNetTwitchBot.Bot.Models.Fishing;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingGameplayService
    {
        Task<FishCatch> PerformFishingAttempt(string userId, string username);
    }
}
