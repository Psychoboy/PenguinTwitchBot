using DotNetTwitchBot.Bot.Commands.Features;
using static DotNetTwitchBot.Bot.Commands.PastyGames.MaxBet;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class MaxBetCalculator
    {
        private readonly ILoyaltyFeature _loyaltyFeature;

        public MaxBetCalculator(ILoyaltyFeature loyaltyFeature)
        {
            _loyaltyFeature = loyaltyFeature;
        }

        public async Task<MaxBet> CheckBetAndRemovePasties(string name, string betAmount, long minBet)
        {
            long amount;
            if (betAmount.Equals("all", StringComparison.CurrentCultureIgnoreCase) ||
               betAmount.Equals("max", StringComparison.CurrentCultureIgnoreCase))
            {
                amount = await _loyaltyFeature.GetMaxPointsFromUser(name);
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

            if (!(await _loyaltyFeature.RemovePointsFromUser(name, amount)))
            {
                return new MaxBet { Result = ParseResult.NotEnough };
            }
            return new MaxBet { Amount = amount, Result = ParseResult.Success };
        }
    }
}
