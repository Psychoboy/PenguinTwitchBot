using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Core;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ExecuteActionHandler(
        IActionManagementService actionService, 
        IAction action,
        IServiceBackbone? serviceBackbone = null) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ExecuteAction;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ExecuteActionType executeAction)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type provided to ExecuteActionHandler: {SubActionType}", subAction.GetType().Name);
            }

            var actionId = executeAction.ActionId;
            if(!actionId.HasValue || actionId.Value == 0)
            {
                throw new SubActionHandlerException(subAction, "Invalid action ID provided to ExecuteActionHandler: {ActionId}", actionId.HasValue ? actionId : "" );
            }

            // Execute the action using the parsed actionId
            var actionItem = await actionService.GetActionByIdAsync(actionId.Value);
            if(actionItem == null)
            {
                throw new SubActionHandlerException(subAction, "No action found with ID: {ActionId}", actionId);
            }

            var childVariables = new ConcurrentDictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase);

            var hasParameters = !string.IsNullOrWhiteSpace(executeAction.Text);
            var isElevated = executeAction.ElevatedCommand;

            if (hasParameters || isElevated)
            {
                var eventArgs = CommandEventArgsConverter.FromDictionaryOrNull(variables);

                if (isElevated)
                {
                    if (!Enum.TryParse(executeAction.RankToExecuteAs, true, out Rank rankToExecuteAs))
                    {
                        throw new SubActionHandlerException(subAction, "Invalid rank specified for elevated action execution: {Rank}", executeAction.RankToExecuteAs ?? "");
                    }

                    var broadcasterName = serviceBackbone?.BroadcasterName ?? string.Empty;

                    if (eventArgs == null)
                    {
                        eventArgs = new Events.Chat.CommandEventArgs
                        {
                            Name = broadcasterName,
                            DisplayName = broadcasterName,
                            IsMod = rankToExecuteAs >= Rank.Moderator,
                            IsBroadcaster = rankToExecuteAs >= Rank.Streamer,
                            IsSub = rankToExecuteAs >= Rank.Subscriber,
                            IsVip = rankToExecuteAs >= Rank.Vip,
                        };
                    }
                    else
                    {
                        eventArgs.IsMod = rankToExecuteAs >= Rank.Moderator || eventArgs.IsMod;
                        eventArgs.IsBroadcaster = rankToExecuteAs >= Rank.Streamer || eventArgs.IsBroadcaster;
                        eventArgs.IsSub = rankToExecuteAs >= Rank.Subscriber || eventArgs.IsSub;
                        eventArgs.IsVip = rankToExecuteAs >= Rank.Vip || eventArgs.IsVip;
                    }
                }
                else if (eventArgs == null)
                {
                    var broadcasterName = serviceBackbone?.BroadcasterName ?? string.Empty;
                    eventArgs = new Events.Chat.CommandEventArgs
                    {
                        Name = broadcasterName,
                        DisplayName = broadcasterName
                    };
                }

                if (hasParameters)
                {
                    var args = VariableReplacer.ReplaceVariables(executeAction.Text, variables);
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
                }

                // Remove existing Args_ keys from childVariables to avoid stale indexed parameters
                var existingArgKeys = childVariables.Keys.Where(k => k.StartsWith("Args_", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var key in existingArgKeys)
                {
                    childVariables.TryRemove(key, out _);
                }

                // Convert eventArgs to dictionary and merge into childVariables
                var convertedDict = CommandEventArgsConverter.ToDictionary(eventArgs);
                foreach (var kvp in convertedDict)
                {
                    childVariables[kvp.Key] = kvp.Value;
                }
            }

            if (context != null && subActionIndex >= 0)
            {
                context.LogMessage(subActionIndex, $"Enqueueing action: {actionItem.Name} to queue: {actionItem.QueueName}");

                // Pass parent context info so the child action can be linked
                await action.EnqueueAction(
                    childVariables, 
                    actionItem,
                    parentLogId: context.ActionLogId,
                    parentSubActionIndex: subActionIndex);

                context.LogMessage(subActionIndex, $"Action enqueued successfully. Child action will be linked when it starts.");
            }
            else
            {
                // No context available, just enqueue normally
                await action.EnqueueAction(childVariables, actionItem);
            }
        }
    }
}
