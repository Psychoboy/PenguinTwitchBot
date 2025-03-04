using MediatR;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;

namespace DotNetTwitchBot.Application.ChatMessage.Notifications
{
    public class DeletedChatMessage : INotification
    {
        public ChannelChatMessageDeleteArgs EventArgs { get; set; } = new();
    }
}
