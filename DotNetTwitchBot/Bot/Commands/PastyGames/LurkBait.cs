using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core.Points;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class LurkBait(IPointsSystem pointsSystem, ILogger<LurkBait> logger) : ILurkBait
    {
        public async Task AwardPoints(LurkBaitTrigger lbtrigger)
        {
            if (lbtrigger.Trigger.Equals("LurkBait Catch", StringComparison.OrdinalIgnoreCase) == false) return;

            logger.LogInformation("{username} caught a {fish} worth {gold} gold with {catchRating} stars and rarity of {rarity}",
                lbtrigger.Username.Replace(Environment.NewLine, ""), 
                lbtrigger.CatchName?.Replace(Environment.NewLine, ""), 
                lbtrigger.CatchValue, 
                lbtrigger.CatchRating, 
                lbtrigger.CatchRarity?.Replace(Environment.NewLine, ""));

            switch(lbtrigger.CatchRating)
            {
                case 3:
                    await pointsSystem.AddPointsByUsernameAndGame(lbtrigger.Username, "lurkbait", lbtrigger.CatchValue * 10000);
                    break;
                case 2:
                    await pointsSystem.AddPointsByUsernameAndGame(lbtrigger.Username, "lurkbait", lbtrigger.CatchValue * 5000);
                    break;
                default:
                    await pointsSystem.AddPointsByUsernameAndGame(lbtrigger.Username, "lurkbait", lbtrigger.CatchValue * 1000);
                    break;
            }
        }
    }
}
