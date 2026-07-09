using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions
{
    public static class SubActionFeatureGate
    {
        private static readonly IReadOnlyDictionary<SubActionTypes, string> FeatureBySubActionType =
            new Dictionary<SubActionTypes, string>
            {
                [SubActionTypes.Fishing] = FeatureKeys.Fishing,
                [SubActionTypes.FishingTournamentStart] = FeatureKeys.Fishing,
                [SubActionTypes.FishingTournamentEnd] = FeatureKeys.Fishing,
                [SubActionTypes.FishingTournamentEligibleCatch] = FeatureKeys.Fishing,
            };

        public static bool IsAvailable(SubActionTypes subActionType, IFeatureRuntimeCoordinator featureRuntimeCoordinator)
        {
            if (!FeatureBySubActionType.TryGetValue(subActionType, out var featureKey))
            {
                return true;
            }

            return featureRuntimeCoordinator.IsEnabled(featureKey);
        }
    }
}