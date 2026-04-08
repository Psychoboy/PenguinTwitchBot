using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Application.Notifications;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class AlertHandlerTests
    {
        [Fact]
        public async Task ValidAlertType_PublishesQueueAlert()
        {
            // Arrange
            var publisher = Substitute.For<INotificationPublisher>();
            var handler = new AlertHandler(publisher);

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
            await publisher.Received(1).Publish(Arg.Is<QueueAlert>(q =>
                q.Alert.Contains("NewFollower followed!")));
            Assert.Equal("NewFollower followed!", alertType.Text);
            Assert.Equal(5, alertType.Duration);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var publisher = Substitute.For<INotificationPublisher>();
            var handler = new AlertHandler(publisher);

            var wrongType = new SendMessageType();  
            var variables = new Dictionary<string, string>();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<SubActionHandlerException>(
                () => handler.ExecuteAsync(wrongType, variables));

            Assert.Contains("is not of AlertType class", exception.Message);
        }
    }
}
