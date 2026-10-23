using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.UfoClaw;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers;

public sealed class UfoClawControlHandler(IUfoClawService service) : ISubActionHandler
{
    public SubActionTypes SupportedType => SubActionTypes.UfoClawControl;
    public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
    {
        if (subAction is not UfoClawControlType control) throw new SubActionHandlerException(subAction, "Invalid UFO claw control type.");
        await service.ControlAsync(control.Operation, control.Mode, VariableReplacer.ReplaceVariables(control.PrizeSetId, variables), new CommandEventArgs
        {
            UserId = variables.GetValueOrDefault("UserId") ?? "",
            Name = variables.GetValueOrDefault("User") ?? variables.GetValueOrDefault("Name") ?? "system",
            DisplayName = variables.GetValueOrDefault("DisplayName") ?? "System"
        });
        context?.LogMessage(subActionIndex, $"UFO Claw: {control.Operation}");
    }
}