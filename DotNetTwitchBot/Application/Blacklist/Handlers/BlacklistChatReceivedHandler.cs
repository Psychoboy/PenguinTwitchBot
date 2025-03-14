﻿
using DotNetTwitchBot.Application.ChatMessage.Notifications;
using MediatR;

namespace DotNetTwitchBot.Application.Blacklist.Handlers
{
    public class BlacklistChatReceivedHandler(Bot.Commands.Moderation.Blacklist blacklist) : INotificationHandler<ReceivedChatMessage>
    {
        public Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            return blacklist.ChatMessage(notification.EventArgs);
        }
    }

    public class BlacklistSuspiciousChatReceivedHandler(Bot.Commands.Moderation.Blacklist blacklist) : INotificationHandler<ReceivedSuspiciousChatMessage>
    {
        public Task Handle(ReceivedSuspiciousChatMessage notification, CancellationToken cancellationToken)
        {
            return blacklist.ChatMessage(notification.EventArgs);
        }
    }
}
