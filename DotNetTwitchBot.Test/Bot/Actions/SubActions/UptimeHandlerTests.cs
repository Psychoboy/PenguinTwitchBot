using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.TwitchServices;
using NSubstitute;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class UptimeHandlerTests
    {
        [Fact]
        public async Task StreamOnline_SetsUptimeVariable()
        {
            // Arrange
            var twitchService = Substitute.For<ITwitchService>();
            var handler = new UptimeHandler(twitchService);

            var startTime = DateTime.UtcNow.AddHours(-3).AddMinutes(-45);
            twitchService.StreamStartedAt().Returns(startTime);

            var uptimeType = new UptimeType();
            var variables = new ConcurrentDictionary<string, string>();

            // Act
            await handler.ExecuteAsync(uptimeType, variables);

            // Assert
            Assert.True(variables.ContainsKey("Uptime"));
            Assert.NotEqual("Offline", variables["Uptime"]);
            Assert.Contains(":", variables["Uptime"]); // Should be time format
        }

        [Fact]
        public async Task StreamOffline_SetsOfflineMessage()
        {
            // Arrange
            var twitchService = Substitute.For<ITwitchService>();
            var handler = new UptimeHandler(twitchService);

            twitchService.StreamStartedAt().Returns(DateTime.MinValue);

            var uptimeType = new UptimeType();
            var variables = new ConcurrentDictionary<string, string>();

            // Act
            await handler.ExecuteAsync(uptimeType, variables);

            // Assert
            Assert.Equal("Offline", variables["Uptime"]);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var twitchService = Substitute.For<ITwitchService>();
            var handler = new UptimeHandler(twitchService);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => handler.ExecuteAsync(wrongType, variables));
        }
    }
}
