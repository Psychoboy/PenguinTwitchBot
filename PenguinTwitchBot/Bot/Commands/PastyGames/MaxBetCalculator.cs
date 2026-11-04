using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Core.Points;
using System.Linq.Expressions;
using static PenguinTwitchBot.Bot.Commands.PastyGames.MaxBet;

namespace PenguinTwitchBot.Bot.Commands.PastyGames
{
    public class MaxBetCalculator(IPointsSystem pointsSystem, IGameSettingsService gameSettingsService)
    {
        public async Task<MaxBet> CheckAndRemovePoints(string userId, string gameName, string betAmount, long minBet)
        {
            var maxAllowedBet = await MaxBetSettings.GetForGameAsync(pointsSystem, gameSettingsService, gameName);
            long amount;
            if (betAmount.Equals("all", StringComparison.OrdinalIgnoreCase) ||
               betAmount.Equals("max", StringComparison.OrdinalIgnoreCase))
            {
                amount = await pointsSystem.GetMaxPointsByUserIdAndGame(userId, gameName, maxAllowedBet);
            }
            else if(betAmount.Contains('%'))
            {
                try
                {
                    var result = new Percentage(betAmount);
                    if (result.Value <= 0 || result.Value > 100)
                    {
                        return CreateResult(ParseResult.InvalidValue, maxAllowedBet);
                    }
                    amount = (long)(await pointsSystem.GetMaxPointsByUserIdAndGame(userId, gameName, maxAllowedBet) * result.Value);
                }
                catch
                {
                    return CreateResult(ParseResult.InvalidValue, maxAllowedBet);
                }
            }
            else if (!Int64.TryParse(betAmount, out amount))
            {
                return CreateResult(ParseResult.InvalidValue, maxAllowedBet);
            }

            if (amount > maxAllowedBet)
            {
                return CreateResult(ParseResult.ToMuch, maxAllowedBet);
            }

            if (amount < minBet)
            {
                return CreateResult(ParseResult.ToLow, maxAllowedBet);
            }

            if (!(await pointsSystem.RemovePointsFromUserByUserIdAndGame(userId, gameName, amount)))
            {
                return CreateResult(ParseResult.NotEnough, maxAllowedBet);
            }
            return new MaxBet { Amount = amount, MaximumAllowedBet = maxAllowedBet, Result = ParseResult.Success };
        }

        private static MaxBet CreateResult(ParseResult result, long maximumAllowedBet) =>
            new() { Result = result, MaximumAllowedBet = maximumAllowedBet };
    }
}
