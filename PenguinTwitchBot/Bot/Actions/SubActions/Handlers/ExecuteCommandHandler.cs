using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Core;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ExecuteCommandHandler(IServiceBackbone serviceBackbone) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ExecuteCommand;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ExecuteCommandType executeCommand)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type provided to ExecuteCommandHandler: {SubActionType}", subAction.GetType().Name);
            }

            if (string.IsNullOrWhiteSpace(executeCommand.CommandName))
            {
                throw new SubActionHandlerException(subAction, "Command name is required for ExecuteCommandHandler.");
            }

            var cleanCommandName = executeCommand.CommandName.Trim().TrimStart('!');

            var eventArgs = CommandEventArgsConverter.FromDictionaryOrNull(variables);
            if (executeCommand.ElevatedCommand)
            {
                if (!Enum.TryParse(executeCommand.RankToExecuteAs, true, out Rank rankToExecuteAs))
                {
                    throw new SubActionHandlerException(subAction, "Invalid rank specified for elevated command execution: {Rank}", executeCommand.RankToExecuteAs ?? "");
                }

                if (eventArgs == null)
                {
                    // Create a new eventArgs if executing outside the context of a command
                    eventArgs = new Events.Chat.CommandEventArgs
                    {
                        Name = serviceBackbone.BroadcasterName,
                        DisplayName = serviceBackbone.BroadcasterName,
                        IsMod = rankToExecuteAs >= Rank.Moderator,
                        IsBroadcaster = rankToExecuteAs >= Rank.Streamer,
                        IsSub = rankToExecuteAs >= Rank.Subscriber,
                        IsVip = rankToExecuteAs >= Rank.Vip,
                    };
                }
                else
                {
                    // Override permissions based on selected rank
                    eventArgs.IsMod = rankToExecuteAs >= Rank.Moderator || eventArgs.IsMod;
                    eventArgs.IsBroadcaster = rankToExecuteAs >= Rank.Streamer || eventArgs.IsBroadcaster;
                    eventArgs.IsSub = rankToExecuteAs >= Rank.Subscriber || eventArgs.IsSub;
                    eventArgs.IsVip = rankToExecuteAs >= Rank.Vip || eventArgs.IsVip;
                }
            }
            else
            {
                if (eventArgs == null)
                {
                    eventArgs = new Events.Chat.CommandEventArgs
                    {
                        Name = serviceBackbone.BroadcasterName,
                        DisplayName = serviceBackbone.BroadcasterName
                    };
                }
            }

            eventArgs.Command = cleanCommandName;
            var args = VariableReplacer.ReplaceVariables(executeCommand.Text, variables);
            eventArgs.Args = string.IsNullOrWhiteSpace(args) ? [] : [.. args.Split(' ', StringSplitOptions.RemoveEmptyEntries)];
            eventArgs.Arg = args;
            if (eventArgs.Args.Count > 0)
            {
                eventArgs.TargetUser = eventArgs.Args[0].TrimStart('@');
            }
            else
            {
                eventArgs.TargetUser = string.Empty;
            }

            if (context != null && subActionIndex >= 0)
            {
                context.LogMessage(subActionIndex, $"Executing command: {cleanCommandName} with parameters: '{args}'");
            }

            await serviceBackbone.RunCommand(eventArgs);
        }
    }
}
