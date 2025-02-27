using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core.Points;
using System.Linq.Expressions;
using static DotNetTwitchBot.Bot.Commands.PastyGames.MaxBet;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class MaxBetCalculator(IPointsSystem pointsSystem)
    {
        public async Task<MaxBet> CheckAndRemovePoints(string userId, string gameName, string betAmount, long minBet)
        {
            long amount;
            if (betAmount.Equals("all", StringComparison.CurrentCultureIgnoreCase) ||
               betAmount.Equals("max", StringComparison.CurrentCultureIgnoreCase))
            {
                amount = await pointsSystem.GetMaxPointsByUserIdAndGame(userId, gameName, PointsSystem.MaxBet);
            }
            else if(betAmount.Contains('%'))
            {
                try
                {
                    var result = new Percentage(betAmount);
                    if (result.Value <= 0 || result.Value > 100)
                    {
                        return new MaxBet
                        {
                            Result = ParseResult.InvalidValue
                        };
                    }
                    amount = (long)(await pointsSystem.GetMaxPointsByUserIdAndGame(userId, gameName, PointsSystem.MaxBet) * result.Value);
                }
                catch
                {
                    return new MaxBet
                    {
                        Result = ParseResult.InvalidValue
                    };
                }
            }
            else if (!Int64.TryParse(betAmount, out amount))
            {
                return new MaxBet
                {
                    Result = ParseResult.InvalidValue
                };
            }

            if (amount > LoyaltyFeature.MaxBet)
            {
                return new MaxBet { Result = ParseResult.ToMuch };
            }

            if (amount < minBet)
            {
                return new MaxBet
                {
                    Result = ParseResult.ToLow
                };
            }

            if (!(await pointsSystem.RemovePointsFromUserByUserIdAndGame(userId, gameName, amount)))
            {
                return new MaxBet { Result = ParseResult.NotEnough };
            }
            return new MaxBet { Amount = amount, Result = ParseResult.Success };
        }
    }
}
