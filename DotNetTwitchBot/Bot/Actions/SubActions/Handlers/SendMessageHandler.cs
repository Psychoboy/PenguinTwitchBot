using DotNetTwitchBot.Application.ChatMessage.Notification;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.TwitchServices;
using MediatR;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class SendMessageHandler(IMediator mediator, ILogger<SendMessageHandler> logger, ITwitchService twitchService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.SendMessage;

        public Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not SendMessageType sendMessageType)
            {
                logger.LogWarning("SubAction with type SendMessage is not of SendMessageType class");
                return Task.CompletedTask;
            }

            sendMessageType.Text = VariableReplacer.ReplaceVariables(sendMessageType.Text, variables);
            if (sendMessageType.UseBot)
            {
                return mediator.Publish(new SendBotMessage(sendMessageType.Text, sendMessageType.StreamOnly));
            } else
            {
                return twitchService.SendMesssageAsStreamer(sendMessageType.Text);
            }
        }
    }
}
