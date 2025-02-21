using DotNetTwitchBot.Bot.Commands.Features;
using System.Linq.Expressions;
using static DotNetTwitchBot.Bot.Commands.PastyGames.MaxBet;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class MaxBetCalculator(ILoyaltyFeature loyaltyFeature)
    {
        public async Task<MaxBet> CheckBetAndRemovePasties(string userId, string betAmount, long minBet)
        {
            long amount;
            if (betAmount.Equals("all", StringComparison.CurrentCultureIgnoreCase) ||
               betAmount.Equals("max", StringComparison.CurrentCultureIgnoreCase))
            {
                amount = await loyaltyFeature.GetMaxPointsFromUserByUserId(userId);
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
                    amount = (long)(await loyaltyFeature.GetMaxPointsFromUserByUserId(userId) * result.Value);
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

            if (!(await loyaltyFeature.RemovePointsFromUserByUserId(userId, amount)))
            {
                return new MaxBet { Result = ParseResult.NotEnough };
            }
            return new MaxBet { Amount = amount, Result = ParseResult.Success };
        }
    }
}
