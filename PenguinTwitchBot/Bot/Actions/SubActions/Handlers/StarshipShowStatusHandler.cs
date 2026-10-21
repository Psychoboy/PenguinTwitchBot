using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class StarshipShowStatusHandler(IStarshipExpeditionService starshipService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.StarshipShowStatus;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not StarshipShowStatusType showSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type StarshipShowStatus is not of StarshipShowStatusType class");
            }

            var resolved = VariableReplacer.ReplaceVariables(showSubAction.DurationSeconds ?? "", variables).Trim();
            if (!int.TryParse(resolved, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) || seconds < 1)
            {
                throw new SubActionHandlerException(subAction, $"Show duration '{resolved}' must be a whole number of seconds (1-3600).");
            }

            var shown = await starshipService.ShowStatusWidgetAsync(seconds);
            context?.LogMessage(subActionIndex, shown == null
                ? "No Starship Expedition to show the status widget for"
                : $"Showing the Starship status widget for {Math.Clamp(seconds, 1, 3600)}s");
        }
    }
}
