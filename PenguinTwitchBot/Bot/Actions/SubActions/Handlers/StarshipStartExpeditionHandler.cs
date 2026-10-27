using System.Collections.Concurrent;
using System.Globalization;
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
        if (subAction is not StarshipStartExpeditionType startSubAction)
            throw new SubActionHandlerException(subAction, "SubAction with type StarshipStartExpedition is not of StarshipStartExpeditionType class");

        var startedBy = variables.TryGetValue("User", out var user) && !string.IsNullOrWhiteSpace(user) ? user : "SubAction";
        var startedByUserId = variables.TryGetValue("UserId", out var userId) ? userId : null;

        int? legCount = ParseOptionalPositiveInt(startSubAction.LegCount, variables);
        int? minInterval = ParseOptionalPositiveInt(startSubAction.MinEventIntervalMinutes, variables);
        int? maxInterval = ParseOptionalPositiveInt(startSubAction.MaxEventIntervalMinutes, variables);
        int? seed = ParseOptionalInt(startSubAction.Seed, variables);

        await starshipService.StartExpeditionAsync(
            startedBy,
            legCount: legCount,
            minimumEventIntervalMinutes: minInterval,
            maximumEventIntervalMinutes: maxInterval,
            seed: seed,
            startedByUserId: startedByUserId);

        context?.LogMessage(subActionIndex, $"Started Starship Expedition for {startedBy}");
    }

    private static int? ParseOptionalPositiveInt(string? rawValue, ConcurrentDictionary<string, string> variables)
    {
        var resolved = VariableReplacer.ReplaceVariables(rawValue ?? "", variables).Trim();
        if (int.TryParse(resolved, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            return parsed;
        }
        return null;
    }

    private static int? ParseOptionalInt(string? rawValue, ConcurrentDictionary<string, string> variables)
    {
        var resolved = VariableReplacer.ReplaceVariables(rawValue ?? "", variables).Trim();
        if (int.TryParse(resolved, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
        return null;
    }
}
