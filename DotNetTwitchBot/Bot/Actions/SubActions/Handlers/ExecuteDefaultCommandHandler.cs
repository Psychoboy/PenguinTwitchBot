using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Core;
using MediatR;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ExecuteDefaultCommandHandler(ILogger<ExecuteDefaultCommandHandler> logger, ICommandHandler commandHandler, IMediator mediator, IServiceBackbone serviceBackbone) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ExecuteDefaultCommand;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if(subAction is not ExecuteDefaultCommandType executeDefaultCommand)
            {
                logger.LogError("Invalid sub action type provided to ExecuteDefaultCommandHandler: {SubActionType}", subAction.GetType().Name);
                return;
            }

            var command = await commandHandler.GetDefaultCommandById(executeDefaultCommand.CommandId);
            if (command == null)
            {
                logger.LogError("No default command found with ID: {CommandId}", executeDefaultCommand.CommandId);
                return;
            }

            var eventArgs = Utilities.CommandEventArgsConverter.FromDictionary(variables);
            if (executeDefaultCommand.ElevatedCommand)
            {

                if (!Enum.TryParse(executeDefaultCommand.RankToExecuteAs, true, out Rank rankToExecuteAs))
                {
                    logger.LogError("Invalid rank specified for elevated command execution: {Rank}", executeDefaultCommand.RankToExecuteAs);
                    return;
                }
                if (string.IsNullOrEmpty(eventArgs.Name))
                {
                    //We create a new eventArgs this happens if this sub action is being executed outside of the context of a command, such as from an action queue. We populate it with the broadcaster's name and the appropriate permissions based on the selected rank to execute as.
                    eventArgs = new Events.Chat.CommandEventArgs
                    {
                        Name = serviceBackbone.BroadcasterName,
                        DisplayName = serviceBackbone.BroadcasterName,
                        IsMod = rankToExecuteAs >= Rank.Moderator,
                        IsBroadcaster = rankToExecuteAs >= Rank.Streamer,
                        IsSub = rankToExecuteAs >= Rank.Subscriber
                    };
                }
            } else
            {
                if(string.IsNullOrEmpty(eventArgs.Name))
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
            eventArgs.Args = [.. args.Split(' ')];
            eventArgs.Arg = args;
            if (eventArgs.Args.Count > 0)
            {
                eventArgs.TargetUser = eventArgs.Args[0];
            }
            else
            {
                eventArgs.TargetUser = string.Empty;
            }

            await serviceBackbone.RunCommand(eventArgs);
        }
    }
}
