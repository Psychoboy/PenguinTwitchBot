using PenguinTwitchBot.Application.ChatMessage.Handlers;
using PenguinTwitchBot.Application.ChatMessage.Notification;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.TwitchServices;
using NSubstitute;
using Xunit;

namespace PenguinTwitchBot.Test.Application.ChatMessage.Handlers
{
    public class SendBotMessageHandlerTests
    {
        [Fact]
        public async Task Handle_InvokesSendMessage()
        {
            var chatBot = Substitute.For<ITwitchChatBot>();
            var handler = new SendBotMessageHandler(chatBot);
            var message = "Hello, chat!";
            var notification = new SendBotMessage(message, true);

            await handler.Handle(notification, CancellationToken.None);

            await chatBot.Received(1).SendMessage(message, true);
        }

        [Fact]
        public async Task Handle_CancellationTokenIsAccepted()
        {
            var chatBot = Substitute.For<ITwitchChatBot>();
            var handler = new SendBotMessageHandler(chatBot);
            var message = "Test message";
            var notification = new SendBotMessage(message, false);
            var cts = new CancellationTokenSource();

            await handler.Handle(notification, cts.Token);

            await chatBot.Received(1).SendMessage(message, false);
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(SendBotMessageHandler).GetMethod("Handle"));
        }
    }

    public class ReplyToMessageHandlerTests
    {
        [Fact]
        public async Task Handle_InvokesReplyToMessage()
        {
            var chatBot = Substitute.For<ITwitchChatBot>();
            var handler = new ReplyToMessageHandler(chatBot);
            var name = "testUser";
            var messageId = "msg123";
            var message = "Reply text";
            var notification = new ReplyToMessage(name, messageId, message);

            await handler.Handle(notification, CancellationToken.None);

            await chatBot.Received(1).ReplyToMessage(name, messageId, message);
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(ReplyToMessageHandler).GetMethod("Handle"));
        }
    }
}