using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using DotNetTwitchBot.Bot.Actions.Utilities;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Core;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ExecuteDefaultCommandHandler(ICommandHandler commandHandler, IServiceBackbone serviceBackbone) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ExecuteDefaultCommand;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if(subAction is not ExecuteDefaultCommandType executeDefaultCommand)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type provided to ExecuteDefaultCommandHandler: {SubActionType}", subAction.GetType().Name);
            }

            var command = await commandHandler.GetDefaultCommandByDefaultCommandName(executeDefaultCommand.CommandName);
            if (command == null)
            {
                throw new SubActionHandlerException(subAction, "No default command found with name: {CommandName}", executeDefaultCommand.CommandName);
            }

            var eventArgs = CommandEventArgsConverter.FromDictionaryOrNull(variables);
            if (executeDefaultCommand.ElevatedCommand)
            {

                if (!Enum.TryParse(executeDefaultCommand.RankToExecuteAs, true, out Rank rankToExecuteAs))
                {
                    throw new SubActionHandlerException(subAction, "Invalid rank specified for elevated command execution: {Rank}", executeDefaultCommand.RankToExecuteAs ?? "");
                }
                if (eventArgs == null)
                {
                    //We create a new eventArgs this happens if this sub action is being executed outside of the context of a command, such as from an action queue. We populate it with the broadcaster's name and the appropriate permissions based on the selected rank to execute as.
                    eventArgs = new Events.Chat.CommandEventArgs
                    {
                        Name = serviceBackbone.BroadcasterName,
                        DisplayName = serviceBackbone.BroadcasterName,
                        IsMod = rankToExecuteAs >= Rank.Moderator,
                        IsBroadcaster = rankToExecuteAs >= Rank.Streamer,
                        IsSub = rankToExecuteAs >= Rank.Subscriber,
                        IsVip = rankToExecuteAs >= Rank.Vip,
                    };
                } else
                {
                    //If there is already a user in the event args, we override their permissions based on the selected rank to execute as.
                    eventArgs.IsMod = rankToExecuteAs >= Rank.Moderator || eventArgs.IsMod;
                    eventArgs.IsBroadcaster = rankToExecuteAs >= Rank.Streamer || eventArgs.IsBroadcaster;
                    eventArgs.IsSub = rankToExecuteAs >= Rank.Subscriber || eventArgs.IsSub;
                    eventArgs.IsVip = rankToExecuteAs >= Rank.Vip || eventArgs.IsVip;
                }
            } else
            {
                if(eventArgs == null)
                {
                    eventArgs = new Events.Chat.CommandEventArgs
                    {
                        Name = serviceBackbone.BroadcasterName,
                        DisplayName = serviceBackbone.BroadcasterName
                    };
                }
            }
            eventArgs.Command = command.CustomCommandName;
            var args = VariableReplacer.ReplaceVariables(executeDefaultCommand.Text, variables);
            eventArgs.Args = string.IsNullOrWhiteSpace(args) ?[] : [.. args.Split(' ', StringSplitOptions.RemoveEmptyEntries)];
            eventArgs.Arg = args;
            if (eventArgs.Args.Count > 0)
            {
                eventArgs.TargetUser = eventArgs.Args[0].TrimStart('@');
            }
            else
            {
                eventArgs.TargetUser = string.Empty;
            }

            await serviceBackbone.RunCommand(eventArgs);
        }
    }
}

