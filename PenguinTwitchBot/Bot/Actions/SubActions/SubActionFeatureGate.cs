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
                [SubActionTypes.FishingGiveItemToPlayer] = FeatureKeys.Fishing,
                [SubActionTypes.FishingTournamentStart] = FeatureKeys.Fishing,
                [SubActionTypes.FishingTournamentEnd] = FeatureKeys.Fishing,
                [SubActionTypes.FishingTournamentEligibleCatch] = FeatureKeys.Fishing,
                [SubActionTypes.FishingModify] = FeatureKeys.Fishing,
                [SubActionTypes.Tts] = FeatureKeys.TTS,
                [SubActionTypes.OpenAi] = FeatureKeys.OpenAI
            };

        public static bool IsAvailable(SubActionTypes subActionType, IFeatureRuntimeCoordinator featureRuntimeCoordinator)
        {
            if (!FeatureBySubActionType.TryGetValue(subActionType, out var featureKey))
            {
                return true;
            }

            if (!featureRuntimeCoordinator.HasFeature(featureKey))
            {
                return false;
            }

            return featureRuntimeCoordinator.IsEnabled(featureKey);
        }
    }
}