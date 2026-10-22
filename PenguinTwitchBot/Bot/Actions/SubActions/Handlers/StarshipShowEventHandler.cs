using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers;

public sealed class StarshipShowEventHandler(IStarshipExpeditionService starshipService) : ISubActionHandler
{
    public SubActionTypes SupportedType => SubActionTypes.StarshipShowEvent;

    public async Task ExecuteAsync(
        SubActionType subAction,
        ConcurrentDictionary<string, string> variables,
        ActionExecutionContext? context = null,
        int subActionIndex = -1)
    {
        if (subAction is not StarshipShowEventType showSubAction)
            throw new SubActionHandlerException(subAction, "SubAction with type StarshipShowEvent is not of StarshipShowEventType class");

        var resolved = VariableReplacer.ReplaceVariables(showSubAction.DurationSeconds ?? string.Empty, variables).Trim();
        if (!int.TryParse(resolved, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) || seconds < 1)
            throw new SubActionHandlerException(subAction, $"Show duration '{resolved}' must be a whole number of seconds (1-3600).");

        var shown = await starshipService.ShowEventWidgetAsync(seconds);
        context?.LogMessage(subActionIndex, shown == null
            ? "No Starship Expedition event to show the event widget for"
            : $"Showing the Starship event widget for {Math.Clamp(seconds, 1, 3600)}s");
    }
}
