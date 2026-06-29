using PenguinTwitchBot.Application.ChatHistory.Handlers;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Channel;
using PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;
using PenguinTwitchBot.TwitchApi.EventSub;
using NSubstitute;
using Xunit;

namespace PenguinTwitchBot.Test.Application.ChatHistory.Handlers
{
    public class DeleteChatMessageHandlerTests
    {
        [Fact]
        public async Task Handle_InvokesDeleteChatMessage()
        {
            var chatHistory = Substitute.For<IChatHistory>();
            var handler = new DeleteChatMessage(chatHistory);
            var eventData = new ChannelChatMessageDelete { MessageId = "test-message-id" };
            var eventArgs = new ChannelChatMessageDeleteEventArgs 
            { 
                Event = eventData,
                Metadata = new ConcreteEventSubMetadata()
            };
            var notification = new DeletedChatMessage { EventArgs = eventArgs };

            await handler.Handle(notification, CancellationToken.None);

            await chatHistory.Received(1).DeleteChatMessage(eventArgs);
        }

        [Fact]
        public async Task Handle_CancellationTokenIsAccepted()
        {
            var chatHistory = Substitute.For<IChatHistory>();
            var handler = new DeleteChatMessage(chatHistory);
            var eventData = new ChannelChatMessageDelete { MessageId = "test-message-id" };
            var eventArgs = new ChannelChatMessageDeleteEventArgs 
            { 
                Event = eventData,
                Metadata = new ConcreteEventSubMetadata()
            };
            var notification = new DeletedChatMessage { EventArgs = eventArgs };
            var cts = new CancellationTokenSource();

            await handler.Handle(notification, cts.Token);

            await chatHistory.Received(1).DeleteChatMessage(eventArgs);
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(DeleteChatMessage).GetMethod("Handle"));
        }
    }

    public class AddChatMessageHandlerTests
    {
        [Fact]
        public async Task Handle_InvokesAddChatMessage()
        {
            var chatHistory = Substitute.For<IChatHistory>();
            var handler = new AddChatMessage(chatHistory);
            var eventArgs = new ChatMessageEventArgs { Name = "testUser", Message = "testMessage" };
            var notification = new ReceivedChatMessage { EventArgs = eventArgs };

            await handler.Handle(notification, CancellationToken.None);

            await chatHistory.Received(1).AddChatMessage(eventArgs);
        }

        [Fact]
        public async Task Handle_CancellationTokenIsAccepted()
        {
            var chatHistory = Substitute.For<IChatHistory>();
            var handler = new AddChatMessage(chatHistory);
            var eventArgs = new ChatMessageEventArgs { Name = "testUser", Message = "testMessage" };
            var notification = new ReceivedChatMessage { EventArgs = eventArgs };
            var cts = new CancellationTokenSource();

            await handler.Handle(notification, cts.Token);

            await chatHistory.Received(1).AddChatMessage(eventArgs);
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(AddChatMessage).GetMethod("Handle"));
        }
    }

    public class ConcreteEventSubMetadata : EventSubMetadata
    {
    }
}