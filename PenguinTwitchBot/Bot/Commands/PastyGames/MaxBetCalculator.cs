using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Core.Points;
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
                if (!TryParseBetPercentage(betAmount, out var fraction))
                {
                    return CreateResult(ParseResult.InvalidValue, maxAllowedBet);
                }

                amount = (long)(await pointsSystem.GetMaxPointsByUserIdAndGame(userId, gameName, maxAllowedBet) * fraction);
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

        /// <summary>
        /// Parses a percentage bet such as "50%" into a fraction (0.5). Only finite values
        /// greater than 0% and up to 100% are accepted.
        /// </summary>
        public static bool TryParseBetPercentage(string betAmount, out double fraction)
        {
            fraction = 0;
            try
            {
                fraction = new Percentage(betAmount).Value;
            }
            catch
            {
                return false;
            }

            return double.IsFinite(fraction) && fraction > 0 && fraction <= 1;
        }

        private static MaxBet CreateResult(ParseResult result, long maximumAllowedBet) =>
            new() { Result = result, MaximumAllowedBet = maximumAllowedBet };
    }
}
