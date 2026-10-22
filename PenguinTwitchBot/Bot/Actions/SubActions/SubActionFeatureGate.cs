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
                [SubActionTypes.OpenAi] = FeatureKeys.OpenAI,
                [SubActionTypes.ParachuteDrop] = FeatureKeys.DropGames,
                [SubActionTypes.ParachuteDroplets] = FeatureKeys.DropGames,
                [SubActionTypes.ParachuteRockets] = FeatureKeys.DropGames,
                [SubActionTypes.PlinkoDrop] = FeatureKeys.DropGames,
                [SubActionTypes.BalloonAscent] = FeatureKeys.DropGames,
                [SubActionTypes.BalloonHazards] = FeatureKeys.DropGames,
                [SubActionTypes.CurlingSlide] = FeatureKeys.DropGames,
                [SubActionTypes.GolfPutt] = FeatureKeys.DropGames,
                [SubActionTypes.StarshipShowStatus] = FeatureKeys.DropGames,
                [SubActionTypes.StarshipShowEvent] = FeatureKeys.DropGames,
                [SubActionTypes.StarshipStartExpedition] = FeatureKeys.DropGames,
                [SubActionTypes.StarshipStopExpedition] = FeatureKeys.DropGames,
                [SubActionTypes.StartDropGameSession] = FeatureKeys.DropGames,
                [SubActionTypes.StopDropGameSession] = FeatureKeys.DropGames
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