using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Models;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public interface IFishingAnalyticsService
    {
        Task<FishingSimulationResult> SimulateFishing(int iterations, List<Guid> shopItemIds);
        Task<Dictionary<Guid, FishProbability>> CalculateCatchProbabilities(List<Guid> shopItemIds);
        Task<Dictionary<Guid, FishProbability>> CalculateCatchProbabilities(bool useBoostMode, double boostModeMultiplier, List<Guid> shopItemIds);
        Task<RarityProbability> CalculateRarityProbabilities(bool useBoostMode, double boostModeMultiplier, List<Guid> shopItemIds);
        Task<FishingBalanceReport> AnalyzeGameBalance(DateTime? startDate = null, DateTime? endDate = null);
        Task<BalanceSimulationResult> SimulateScenario(BalanceSimulationScenario scenario);
        Task<ProjectedEconomyModel> CalculateProjectedEconomy();
        Task<List<ItemEconomyAnalysis>> CalculateItemEconomyAnalysis();
        Task<double> CalculateBaselineExpectedGold();
        Task<double> CalculateProgressiveBaselineGold(int targetWeeks = 26);
        Task<Dictionary<string, int>> CalculateRecommendedPricing(int targetWeeksForEndgame = 26);
    }
}
