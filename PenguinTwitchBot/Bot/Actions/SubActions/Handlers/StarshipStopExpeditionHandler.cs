using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers;

public sealed class StarshipStopExpeditionHandler(IStarshipExpeditionService starshipService) : ISubActionHandler
{
    public SubActionTypes SupportedType => SubActionTypes.StarshipStopExpedition;

    public async Task ExecuteAsync(
        SubActionType subAction,
        ConcurrentDictionary<string, string> variables,
        ActionExecutionContext? context = null,
        int subActionIndex = -1)
    {
        if (subAction is not StarshipStopExpeditionType)
            throw new SubActionHandlerException(subAction, "SubAction with type StarshipStopExpedition is not of StarshipStopExpeditionType class");

        var stoppedBy = variables.TryGetValue("User", out var user) && !string.IsNullOrWhiteSpace(user) ? user : "SubAction";
        var stopped = await starshipService.StopExpeditionAsync(stoppedBy);
        context?.LogMessage(subActionIndex, stopped == null
            ? "No active Starship Expedition to stop"
            : $"Stopped Starship Expedition for {stoppedBy}");
    }
}
