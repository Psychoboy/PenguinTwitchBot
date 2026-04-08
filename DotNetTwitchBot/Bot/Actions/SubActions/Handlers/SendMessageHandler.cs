using DotNetTwitchBot.Application.ChatMessage.Notification;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class SendMessageHandler(Application.Notifications.IPenguinDispatcher dispatcher, ITwitchService twitchService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.SendMessage;

        public Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not SendMessageType sendMessageType)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type SendMessage is not of SendMessageType class");
            }

            sendMessageType.Text = VariableReplacer.ReplaceVariables(sendMessageType.Text, variables);
            if (sendMessageType.UseBot)
            {
                return dispatcher.Publish(new SendBotMessage(sendMessageType.Text, sendMessageType.StreamOnly));
            } else
            {
                return twitchService.SendMesssageAsStreamer(sendMessageType.Text);
            }
        }
    }
}
