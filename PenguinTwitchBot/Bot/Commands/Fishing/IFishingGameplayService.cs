using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingGameplayService
    {
        Task<FishingAttemptResult> PerformFishingAttempt(string userId, string username);
    }
}
