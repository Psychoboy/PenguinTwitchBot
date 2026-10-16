using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ParachuteDropletsHandler(IParachuteDropService parachuteDropService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ParachuteDroplets;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not ParachuteDropletsType dropletsSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type ParachuteDroplets is not of ParachuteDropletsType class");
            }

            var resolvedEmote = VariableReplacer.ReplaceVariables(dropletsSubAction.EmoteOrImage ?? "", variables).Trim();
            if (resolvedEmote.Equals("%Args%", StringComparison.OrdinalIgnoreCase))
            {
                resolvedEmote = "";
            }

            var triggeredBy = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "Action";
            var eventArgs = CommandEventArgsConverter.FromDictionaryOrNull(variables);

            context?.LogMessage(
                subActionIndex,
                $"Spawning {dropletsSubAction.Count} Parachute Droplets (Size={dropletsSubAction.SizePreset}, Speed={dropletsSubAction.SpeedPreset}, Pattern={dropletsSubAction.SpawnPattern})");

            await parachuteDropService.SpawnDropletsAsync(new DropletSpawnRequest
            {
                Count = dropletsSubAction.Count,
                SizePreset = dropletsSubAction.SizePreset,
                SpeedPreset = dropletsSubAction.SpeedPreset,
                SpawnPattern = dropletsSubAction.SpawnPattern,
                KnockbackMultiplier = dropletsSubAction.KnockbackMultiplier,
                EmoteOrImage = resolvedEmote,
                NativeEmoteUrl = eventArgs?.EmoteUrl,
                Fragments = eventArgs?.Fragments,
                AllowDirectUrlOrPath = true,
                TriggeredByUser = triggeredBy
            });
        }
    }
}
