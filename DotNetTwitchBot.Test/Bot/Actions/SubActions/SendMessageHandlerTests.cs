using DotNetTwitchBot.Application.ChatMessage.Notification;
using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Application.Notifications;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class SendMessageHandlerTests
    {
        [Fact]
        public async Task ValidSendMessageType_PublishesSendBotMessage()
        {
            // Arrange
            var publisher = Substitute.For<INotificationPublisher>();
            var twitchService = Substitute.For<DotNetTwitchBot.Bot.TwitchServices.ITwitchService>();
            var handler = new SendMessageHandler(publisher, twitchService);

            var sendMessageType = new SendMessageType
            {
                Text = "Hello %user%!",
                UseBot = true,
                StreamOnly = false
            };

            var variables = new Dictionary<string, string> { { "user", "TestUser" } };

            // Act
            await handler.ExecuteAsync(sendMessageType, variables);

            // Assert
            await publisher.Received(1).Publish(Arg.Is<SendBotMessage>(m =>
                m.Message == "Hello TestUser!" &&
                m.SourceOnly == false));
        }

        [Fact]
        public async Task UseBotFalse_DoesNotPublish()
        {
            // Arrange
            var publisher = Substitute.For<INotificationPublisher>();
            var twitchService = Substitute.For<DotNetTwitchBot.Bot.TwitchServices.ITwitchService>();
            var handler = new SendMessageHandler(publisher, twitchService);

            var sendMessageType = new SendMessageType
            {
                Text = "Test",
                UseBot = false
            };

            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(sendMessageType, variables);

            // Assert
            await publisher.DidNotReceive().Publish(Arg.Any<SendBotMessage>());
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var publisher = Substitute.For<INotificationPublisher>();
            var twitchService = Substitute.For<DotNetTwitchBot.Bot.TwitchServices.ITwitchService>();
            var handler = new SendMessageHandler(publisher, twitchService);

            var wrongType = new CurrentTimeType();
            var variables = new Dictionary<string, string>();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<SubActionHandlerException>(
                () => handler.ExecuteAsync(wrongType, variables));

            Assert.Contains("is not of SendMessageType class", exception.Message);
        }
    }
}
