using DotNetTwitchBot.Bot.Models.Fishing;
using DotNetTwitchBot.Models;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingAnalyticsService
    {
        Task<FishingSimulationResult> SimulateFishing(int iterations, bool useBoostMode, double boostModeMultiplier, List<int> shopItemIds);
        Task<Dictionary<int, FishProbability>> CalculateCatchProbabilities(List<int> shopItemIds);
        Task<Dictionary<int, FishProbability>> CalculateCatchProbabilities(bool useBoostMode, double boostModeMultiplier, List<int> shopItemIds);
        Task<RarityProbability> CalculateRarityProbabilities(bool useBoostMode, double boostModeMultiplier, List<int> shopItemIds);
        Task<FishingBalanceReport> AnalyzeGameBalance(DateTime? startDate = null, DateTime? endDate = null);
        Task<double> CalculateBaselineExpectedGold();
        Task<double> CalculateProgressiveBaselineGold(int targetWeeks = 26);
    }
}
