using DotNetTwitchBot.Application.ChatMessage.Notification;
using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using MediatR;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class SendMessageHandlerTests
    {
        [Fact]
        public async Task ValidSendMessageType_PublishesSendBotMessage()
        {
            // Arrange
            var mediator = Substitute.For<IMediator>();
            var twitchService = Substitute.For<DotNetTwitchBot.Bot.TwitchServices.ITwitchService>();
            var handler = new SendMessageHandler(mediator, twitchService);

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
            await mediator.Received(1).Publish(Arg.Is<SendBotMessage>(m =>
                m.Message == "Hello TestUser!" &&
                m.SourceOnly == false));
        }

        [Fact]
        public async Task UseBotFalse_DoesNotPublish()
        {
            // Arrange
            var mediator = Substitute.For<IMediator>();
            var twitchService = Substitute.For<DotNetTwitchBot.Bot.TwitchServices.ITwitchService>();
            var handler = new SendMessageHandler(mediator, twitchService);

            var sendMessageType = new SendMessageType
            {
                Text = "Test",
                UseBot = false
            };

            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(sendMessageType, variables);

            // Assert
            await mediator.DidNotReceive().Publish(Arg.Any<SendBotMessage>());
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var mediator = Substitute.For<IMediator>();
            var twitchService = Substitute.For<DotNetTwitchBot.Bot.TwitchServices.ITwitchService>();
            var handler = new SendMessageHandler(mediator, twitchService);

            var wrongType = new CurrentTimeType();
            var variables = new Dictionary<string, string>();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<SubActionHandlerException>(
                () => handler.ExecuteAsync(wrongType, variables));

            Assert.Contains("is not of SendMessageType class", exception.Message);
        }
    }
}
