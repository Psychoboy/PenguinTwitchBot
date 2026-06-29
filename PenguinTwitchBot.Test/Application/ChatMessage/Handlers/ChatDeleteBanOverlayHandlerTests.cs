using PenguinTwitchBot.Application.ChatMessage.Handlers;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Channel;
using PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;
using PenguinTwitchBot.TwitchApi.EventSub;
using NSubstitute;
using Xunit;
using System.Text.Json;

namespace PenguinTwitchBot.Test.Application.ChatMessage.Handlers
{
    public class ChatDeleteOverlayHandlerTests
    {
        [Fact]
        public async Task Handle_SerializesCorrectPayload()
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var handler = new ChatDeleteOverlayHandler(webSocketMessenger);
            var eventData = new ChannelChatMessageDelete { MessageId = "msg123" };
            var eventArgs = new ChannelChatMessageDeleteEventArgs 
            { 
                Event = eventData,
                Metadata = new ConcreteEventSubMetadata()
            };
            var notification = new DeletedChatMessage { EventArgs = eventArgs };

            await handler.Handle(notification, CancellationToken.None);

            var expectedPayload = new { type = "chat_delete", id = "msg123" };
            var expectedJson = JsonSerializer.Serialize(expectedPayload, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            await webSocketMessenger.Received(1).AddToQueue(expectedJson);
        }

        [Fact]
        public async Task Handle_CancellationTokenIsAccepted()
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var handler = new ChatDeleteOverlayHandler(webSocketMessenger);
            var eventData = new ChannelChatMessageDelete { MessageId = "test" };
            var notification = new DeletedChatMessage 
            { 
                EventArgs = new ChannelChatMessageDeleteEventArgs 
                { 
                    Event = eventData,
                    Metadata = new ConcreteEventSubMetadata()
                }
            };
            var cts = new CancellationTokenSource();

            await handler.Handle(notification, cts.Token);

            await webSocketMessenger.Received(1).AddToQueue(Arg.Any<string>());
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(ChatDeleteOverlayHandler).GetMethod("Handle"));
        }
    }

    public class ChatBanOverlayHandlerTests
    {
        [Fact]
        public async Task Handle_SerializesCorrectPayload()
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var handler = new ChatBanOverlayHandler(webSocketMessenger);
            var userId = "user123";
            var notification = new BannedChatUser { UserId = userId };

            await handler.Handle(notification, CancellationToken.None);

            var expectedPayload = new { type = "chat_user_banned", userId = userId };
            var expectedJson = JsonSerializer.Serialize(expectedPayload, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            await webSocketMessenger.Received(1).AddToQueue(expectedJson);
        }

        [Fact]
        public async Task Handle_CancellationTokenIsAccepted()
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var handler = new ChatBanOverlayHandler(webSocketMessenger);
            var notification = new BannedChatUser { UserId = "test" };
            var cts = new CancellationTokenSource();

            await handler.Handle(notification, cts.Token);

            await webSocketMessenger.Received(1).AddToQueue(Arg.Any<string>());
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(ChatBanOverlayHandler).GetMethod("Handle"));
        }
    }

    public class ConcreteEventSubMetadata : EventSubMetadata
    {
    }
}