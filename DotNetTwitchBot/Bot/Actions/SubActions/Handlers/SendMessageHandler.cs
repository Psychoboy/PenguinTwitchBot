using DotNetTwitchBot.Application.ChatMessage.Notification;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using MediatR;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class SendMessageHandler(IMediator mediator, ILogger<SendMessageHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.SendMessage;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not SendMessageType sendMessageType)
            {
                logger.LogWarning("SubAction with type SendMessage is not of SendMessageType class");
                return;
            }

            sendMessageType.Text = VariableReplacer.ReplaceVariables(sendMessageType.Text, variables);
            if (sendMessageType.UseBot)
            {
                await mediator.Publish(new SendBotMessage(sendMessageType.Text, sendMessageType.StreamOnly));
            }
        }
    }
}
