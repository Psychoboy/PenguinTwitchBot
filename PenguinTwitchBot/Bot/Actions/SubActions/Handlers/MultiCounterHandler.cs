using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Bot.Services;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers;

public class MultiCounterHandler(ICounterService counterService) : ISubActionHandler
{
    public SubActionTypes SupportedType => SubActionTypes.MultiCounter;

    public async Task ExecuteAsync(
        SubActionType subAction,
        ConcurrentDictionary<string, string> variables,
        ActionExecutionContext? context = null,
        int subActionIndex = -1)
    {
        if (subAction is not MultiCounterType multiCounterSubAction)
        {
            throw new SubActionHandlerException(subAction, "Invalid sub action type. Expected MultiCounterType.");
        }

        var counterName = multiCounterSubAction.Name;
        if (string.IsNullOrWhiteSpace(counterName))
        {
            return;
        }

        var eventArgs = variables.ContainsKey("OriginalEventArgs") ? CommandEventArgsConverter.FromDictionary(variables) : null;
        CounterResult result;

        if (multiCounterSubAction.Operation == CounterOperation.CommandArgs)
        {
            result = await counterService.EvaluateCommandArgsAsync(
                counterName,
                eventArgs,
                multiCounterSubAction.Min,
                multiCounterSubAction.Max);
        }
        else
        {
            result = await counterService.AdjustCounterAsync(
                counterName,
                multiCounterSubAction.Operation,
                multiCounterSubAction.Value,
                multiCounterSubAction.Min,
                multiCounterSubAction.Max,
                eventArgs);
        }

        counterService.PopulateVariables(variables, result, multiCounterSubAction.DestinationVariable);
    }
}
