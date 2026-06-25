using PenguinTwitchBot.Application.ChatMessage.Notification;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.TwitchServices;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ReplyToMessageHandler(ITwitchChatBot chatBot, Application.Notifications.IPenguinDispatcher dispatcher, ITwitchService twitchService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ReplyToMessage;

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if(subAction is not ReplyToMessageType replyToMessage)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for ReplyToMessageHandler: {SubActionType}", subAction.GetType().Name);
            }

            replyToMessage.Text = VariableReplacer.ReplaceVariables(replyToMessage.Text, variables);

            if (variables.TryGetValue("OriginalEventArgs", out var originalEventArgs) && JsonUtils.DeserializeJson(originalEventArgs, out ChatMessageEventArgs? eventArgs) && eventArgs != null 
                && !string.IsNullOrWhiteSpace(eventArgs.MessageId))
            {
                return chatBot.ReplyToMessage(eventArgs.Name, eventArgs.MessageId, replyToMessage.Text, replyToMessage.StreamOnly);
            }
            else if (replyToMessage.UseBot)
            {
                return dispatcher.Publish(new SendBotMessage(replyToMessage.Text, replyToMessage.StreamOnly));
            }
            else
            {
                return twitchService.SendMesssageAsStreamer(replyToMessage.Text);
            }
        }
    }
}
