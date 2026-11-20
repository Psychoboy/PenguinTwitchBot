using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class FishingFrenzyHandler(IFishingFrenzyService frenzyService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.FishingFrenzy;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not FishingFrenzyType frenzyAction)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for FishingFrenzyHandler");
            }

            if (string.Equals(frenzyAction.Mode, "Stop", StringComparison.OrdinalIgnoreCase))
            {
                await frenzyService.StopFrenzyAsync(frenzyAction.AnnounceInChat);
                variables["fishing_frenzy_active"] = "false";
                return;
            }

            var resolvedName = VariableReplacer.ReplaceVariables(frenzyAction.FrenzyName, variables);
            var resolvedCategory = VariableReplacer.ReplaceVariables(frenzyAction.TargetCategory, variables);

            var state = await frenzyService.StartFrenzyAsync(
                string.IsNullOrWhiteSpace(resolvedName) ? "Fishing Frenzy" : resolvedName,
                frenzyAction.DurationMinutes,
                frenzyAction.RarityMultiplier,
                string.IsNullOrWhiteSpace(resolvedCategory) ? null : resolvedCategory,
                frenzyAction.CategoryBoost,
                frenzyAction.AnnounceInChat);

            variables["fishing_frenzy_active"] = "true";
            variables["fishing_frenzy_name"] = state.Name;
            variables["fishing_frenzy_ends_utc"] = state.EndsAtUtc.ToString("O");
        }
    }
}
