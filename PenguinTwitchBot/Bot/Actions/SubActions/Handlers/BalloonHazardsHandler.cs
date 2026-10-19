using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.Balloon;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class BalloonHazardsHandler(IBalloonAscentService balloonAscentService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.BalloonHazards;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not BalloonHazardsType hazardSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type BalloonHazards is not of BalloonHazardsType class");
            }

            var resolvedEmote = VariableReplacer.ReplaceVariables(hazardSubAction.EmoteOrImage ?? "", variables).Trim();
            if (resolvedEmote.Equals("%Args%", StringComparison.OrdinalIgnoreCase))
            {
                resolvedEmote = "";
            }

            var resolvedTargetUser = VariableReplacer.ReplaceVariables(hazardSubAction.TargetUser ?? "", variables).Trim();
            if (resolvedTargetUser.Equals("%User%", StringComparison.OrdinalIgnoreCase))
            {
                resolvedTargetUser = variables.TryGetValue("User", out var u) ? u : "";
            }

            var request = new BalloonHazardSpawnRequest
            {
                Count = hazardSubAction.Count,
                HazardType = hazardSubAction.HazardType,
                Pattern = hazardSubAction.SpawnPattern,
                SpeedPreset = hazardSubAction.SpeedPreset,
                SizePreset = hazardSubAction.SizePreset,
                EmoteOrImage = resolvedEmote,
                TargetUser = string.IsNullOrWhiteSpace(resolvedTargetUser) ? null : resolvedTargetUser,
                TriggeredBy = variables.TryGetValue("User", out var trigUser) ? trigUser : "SubAction"
            };

            context?.LogMessage(subActionIndex, $"Spawning {request.Count} Balloon hazards ({request.HazardType})");
            await balloonAscentService.SpawnHazardsAsync(request);
        }
    }
}

