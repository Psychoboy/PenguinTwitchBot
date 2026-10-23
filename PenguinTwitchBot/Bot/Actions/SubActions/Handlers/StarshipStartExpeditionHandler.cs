using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers;

public sealed class StarshipStartExpeditionHandler(IStarshipExpeditionService starshipService) : ISubActionHandler
{
    public SubActionTypes SupportedType => SubActionTypes.StarshipStartExpedition;

    public async Task ExecuteAsync(
        SubActionType subAction,
        ConcurrentDictionary<string, string> variables,
        ActionExecutionContext? context = null,
        int subActionIndex = -1)
    {
        if (subAction is not StarshipStartExpeditionType)
            throw new SubActionHandlerException(subAction, "SubAction with type StarshipStartExpedition is not of StarshipStartExpeditionType class");

        var startedBy = variables.TryGetValue("User", out var user) && !string.IsNullOrWhiteSpace(user) ? user : "SubAction";
        var startedByUserId = variables.TryGetValue("UserId", out var userId) ? userId : null;
        await starshipService.StartExpeditionAsync(startedBy, startedByUserId: startedByUserId);
        context?.LogMessage(subActionIndex, $"Started Starship Expedition for {startedBy} using saved game settings");
    }
}
