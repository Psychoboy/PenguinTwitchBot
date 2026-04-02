using DotNetTwitchBot.Application.ChatMessage.Notification;
using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using MediatR;
using Microsoft.Extensions.Logging;
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
            var logger = Substitute.For<ILogger<SendMessageHandler>>();
            var handler = new SendMessageHandler(mediator, logger);

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
            var logger = Substitute.For<ILogger<SendMessageHandler>>();
            var handler = new SendMessageHandler(mediator, logger);

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
        public async Task WrongType_LogsWarning()
        {
            // Arrange
            var mediator = Substitute.For<IMediator>();
            var logger = Substitute.For<ILogger<SendMessageHandler>>();
            var handler = new SendMessageHandler(mediator, logger);

            var wrongType = new SubActionType();
            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(wrongType, variables);

            // Assert
            logger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("is not of SendMessageType class")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
