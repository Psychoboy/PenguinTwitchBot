﻿using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core.Points;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class LurkBait(IPointsSystem pointsSystem, ILogger<LurkBait> logger) : ILurkBait
    {
        public async Task AwardPoints(LurkBaitTrigger lbtrigger)
        {
            if (lbtrigger.Trigger.Equals("LurkBait Catch", StringComparison.CurrentCultureIgnoreCase) == false) return;

            logger.LogInformation("{username} caught a {fish} worth {gold} gold with {catchRating} stars and rarity of {rarity}",
                lbtrigger.Username, lbtrigger.CatchName, lbtrigger.CatchValue, lbtrigger.CatchRating, lbtrigger.CatchRarity);

            await pointsSystem.AddPointsByUsernameAndGame(lbtrigger.Username, "lurkbait", lbtrigger.CatchValue * lbtrigger.CatchRating * 10);
        }
    }
}
