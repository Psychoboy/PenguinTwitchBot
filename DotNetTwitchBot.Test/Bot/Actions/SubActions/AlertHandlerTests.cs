using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class AlertHandlerTests
    {
        [Fact]
        public async Task ValidAlertType_PublishesQueueAlert()
        {
            // Arrange
            var mediator = Substitute.For<IMediator>();
            var logger = Substitute.For<ILogger<AlertHandler>>();
            var handler = new AlertHandler(mediator, logger);

            var alertType = new AlertType
            {
                Text = "%user% followed!",
                File = "alert.png",
                Duration = 5,
                Volume = 0.8f,
                CSS = "color: red;"
            };

            var variables = new Dictionary<string, string> { { "user", "NewFollower" } };

            // Act
            await handler.ExecuteAsync(alertType, variables);

            // Assert
            await mediator.Received(1).Publish(Arg.Is<QueueAlert>(q =>
                q.Alert.Contains("NewFollower followed!")));
            Assert.Equal("NewFollower followed!", alertType.Text);
            Assert.Equal(5, alertType.Duration);
        }

        [Fact]
        public async Task WrongType_LogsWarning()
        {
            // Arrange
            var mediator = Substitute.For<IMediator>();
            var logger = Substitute.For<ILogger<AlertHandler>>();
            var handler = new AlertHandler(mediator, logger);

            var wrongType = new SubActionType();
            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(wrongType, variables);

            // Assert
            logger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("is not of AlertType class")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
