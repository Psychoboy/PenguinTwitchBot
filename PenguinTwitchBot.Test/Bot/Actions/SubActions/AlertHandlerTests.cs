using PenguinTwitchBot.Application.Alert.Notification;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class AlertHandlerTests
    {
        [Fact]
        public async Task ValidAlertType_PublishesQueueAlert()
        {
            // Arrange
            var dispatcher = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();
            var handler = new AlertHandler(dispatcher);

            var alertType = new AlertType
            {
                Text = "%user% followed!",
                File = "alert.png",
                Duration = 5,
                Volume = 0.8f,
                CSS = "color: red;"
            };

            var variables = new ConcurrentDictionary<string, string> { ["user"] = "NewFollower" };

            // Act
            await handler.ExecuteAsync(alertType, variables);

            // Assert
            await dispatcher.Received(1).Publish(Arg.Is<QueueAlert>(q =>
                q.Alert.Contains("NewFollower followed!")));
            Assert.Equal("NewFollower followed!", alertType.Text);
            Assert.Equal(5, alertType.Duration);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var dispatcher = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();
            var handler = new AlertHandler(dispatcher);

            var wrongType = new SendMessageType();  
            var variables = new ConcurrentDictionary<string, string>();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<SubActionHandlerException>(
                () => handler.ExecuteAsync(wrongType, variables));

            Assert.Contains("is not of AlertType class", exception.Message);
        }

        [Fact]
        public void AlertType_Generate_WithQuotesAndSpecialCharacters_ProducesValidJson()
        {
            var alert = new AlertType
            {
                Text = "Hello \"world\"! Here is a \\ slash and \", \"injected\": true",
                File = "alert.gif",
                Duration = 4,
                Volume = 0.5f,
                CSS = "color: blue;",
                AlertChannel = "test-channel"
            };

            var json = alert.Generate();

            // Must deserialize cleanly without JSON syntax error
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("alert_image", out var alertImageProp));
            Assert.Contains("Hello \"world\"!", alertImageProp.GetString());
            Assert.True(root.TryGetProperty("ignoreIsPlaying", out var ignoreProp));
            Assert.False(ignoreProp.GetBoolean());
            Assert.True(root.TryGetProperty("alertChannel", out var channelProp));
            Assert.Equal("test-channel", channelProp.GetString());
            Assert.False(root.TryGetProperty("injected", out _));
        }
    }
}
