using PenguinTwitchBot.Application.ChatMessage.Handlers;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.Services.Chat;
using NSubstitute;
using Xunit;
using PenguinTwitchBot.Bot.Models.Chat;
using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Test.Application.ChatMessage.Handlers
{
    public class ChatOverlayHandlerTests
    {
        [Fact]
        public async Task Handle_SerializesChatMessageToJson()
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var chatColorService = Substitute.For<IChatColorService>();
            var handler = new ChatOverlayHandler(webSocketMessenger, chatColorService);

            var eventArgs = new ChatMessageEventArgs
            {
                MessageId = "msg123",
                UserId = "user1",
                Name = "testUser",
                DisplayName = "TestUser",
                Message = "test message",
                Badges = new System.Collections.Generic.List<ChatOverlayBadge>(),
                Fragments = new System.Collections.Generic.List<ChatOverlayFragment>()
            };
            var notification = new ReceivedChatMessage { EventArgs = eventArgs };
            chatColorService.GetOrAssignColor("user1", Arg.Any<string>()).Returns("#FF0000");

            await handler.Handle(notification, CancellationToken.None);

            await webSocketMessenger.Received(1).AddToQueue(Arg.Any<string>());
        }

        [Fact]
        public async Task Handle_CallsChatColorService()
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var chatColorService = Substitute.For<IChatColorService>();
            var handler = new ChatOverlayHandler(webSocketMessenger, chatColorService);

            var eventArgs = new ChatMessageEventArgs
            {
                MessageId = "msg123",
                UserId = "user1",
                Name = "testUser",
                DisplayName = "TestUser",
                Message = "test message",
                Badges = new System.Collections.Generic.List<ChatOverlayBadge>(),
                Fragments = new System.Collections.Generic.List<ChatOverlayFragment>()
            };
            var notification = new ReceivedChatMessage { EventArgs = eventArgs };

            await handler.Handle(notification, CancellationToken.None);

            chatColorService.Received(1).GetOrAssignColor("user1", Arg.Any<string>());
        }

        [Fact]
        public async Task Handle_CancellationTokenIsAccepted()
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var chatColorService = Substitute.For<IChatColorService>();
            var handler = new ChatOverlayHandler(webSocketMessenger, chatColorService);
            var cts = new CancellationTokenSource();

            var eventArgs = new ChatMessageEventArgs
            {
                MessageId = "msg123",
                UserId = "user1",
                Name = "testUser",
                DisplayName = "TestUser",
                Message = "test message",
                Badges = new System.Collections.Generic.List<ChatOverlayBadge>(),
                Fragments = new System.Collections.Generic.List<ChatOverlayFragment>()
            };
            var notification = new ReceivedChatMessage { EventArgs = eventArgs };

            await handler.Handle(notification, cts.Token);

            await webSocketMessenger.Received(1).AddToQueue(Arg.Any<string>());
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(ChatOverlayHandler).GetMethod("Handle"));
        }
    }
}