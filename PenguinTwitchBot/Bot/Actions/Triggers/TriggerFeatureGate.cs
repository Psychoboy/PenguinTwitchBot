using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Triggers
{
    public static class TriggerFeatureGate
    {
        private static readonly IReadOnlyDictionary<TriggerTypes, string> FeatureByTriggerType =
            new Dictionary<TriggerTypes, string>
            {
                [TriggerTypes.Timer] = FeatureKeys.AutoTimer,
                [TriggerTypes.FishingTournamentCatch] = FeatureKeys.Fishing,
                [TriggerTypes.FishingTournamentStart] = FeatureKeys.Fishing,
                [TriggerTypes.FishingTournamentEnd] = FeatureKeys.Fishing,
                [TriggerTypes.FishCatch] = FeatureKeys.Fishing
            };

        public static bool IsAvailable(TriggerTypes triggerType, IFeatureRuntimeCoordinator featureRuntimeCoordinator)
        {
            if (!FeatureByTriggerType.TryGetValue(triggerType, out var featureKey))
            {
                return true;
            }

            return featureRuntimeCoordinator.IsEnabled(featureKey);
        }
    }
}