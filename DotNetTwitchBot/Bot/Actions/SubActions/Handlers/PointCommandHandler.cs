using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using DotNetTwitchBot.Bot.Actions.Utilities;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class PointCommandHandler(IPointsSystem pointsSystem, IServiceBackbone serviceBackbone) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ExecutePointCommand;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not PointCommandType pointCommandType)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type passed to PointCommandHandler");
            }

            var commandText = VariableReplacer.ReplaceVariables(pointCommandType.Text, variables);
            var pointCommand = await pointsSystem.GetPointCommand(commandText) ?? throw new SubActionHandlerException(subAction, "Point command not found: {CommandText}", commandText);
            var eventArgs = CommandEventArgsConverter.FromDictionaryOrNull(variables);
            if (pointCommandType.ElevatedCommand)
            {

                if (!Enum.TryParse(pointCommandType.RankToExecuteAs, true, out Rank rankToExecuteAs))
                {
                    throw new SubActionHandlerException(subAction, "Invalid rank specified for elevated command execution: {Rank}", pointCommandType.RankToExecuteAs ?? "");
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
                }
                else
                {
                    //If there is already a user in the event args, we override their permissions based on the selected rank to execute as.
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

            eventArgs.Command = pointCommand.CommandName;
            var args = VariableReplacer.ReplaceVariables(pointCommandType.Arguments, variables);
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

            if (pointCommandType.RespondToChat)
            {

                await serviceBackbone.RunCommand(eventArgs);
            }
            else
            {
                await pointsSystem.RunFromActionNoResponse(eventArgs, pointCommand, variables);
            }
        }
    }
}

