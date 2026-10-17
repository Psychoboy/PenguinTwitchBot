using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ParachuteRocketsHandler(IParachuteDropService parachuteDropService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ParachuteRockets;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not ParachuteRocketsType rocketsSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type ParachuteRockets is not of ParachuteRocketsType class");
            }

            var resolvedTarget = VariableReplacer.ReplaceVariables(rocketsSubAction.TargetUser ?? "", variables).Trim();
            if (resolvedTarget.Equals("%Args%", StringComparison.OrdinalIgnoreCase) ||
                resolvedTarget.Equals("{Target}", StringComparison.OrdinalIgnoreCase))
            {
                resolvedTarget = variables.TryGetValue("Target", out var t) ? t : "";
            }

            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "SubAction";

            CrashLandingScoreMode? scoreMode = null;
            if (Enum.TryParse<CrashLandingScoreMode>(rocketsSubAction.CrashLandingScoreMode, true, out var parsedMode))
            {
                scoreMode = parsedMode;
            }

            context?.LogMessage(
                subActionIndex,
                $"Launching {rocketsSubAction.Count} Sky Rockets (Pattern={rocketsSubAction.Pattern}, Target={resolvedTarget}, KillChance={rocketsSubAction.CanopyKillChance}%)");

            await parachuteDropService.FireRocketsAsync(new RocketFireRequest
            {
                Count = rocketsSubAction.Count,
                Pattern = rocketsSubAction.Pattern,
                TargetUser = string.IsNullOrWhiteSpace(resolvedTarget) ? null : resolvedTarget,
                CanopyKillChance = rocketsSubAction.CanopyKillChance,
                KnockbackMultiplier = rocketsSubAction.KnockbackMultiplier,
                ScoreModeOverride = scoreMode,
                TriggeredByUser = triggeredBy
            });
        }
    }
}

