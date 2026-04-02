using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using DotNetTwitchBot.Bot.TwitchServices;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class UptimeHandlerTests
    {
        [Fact]
        public async Task StreamOnline_SetsUptimeVariable()
        {
            // Arrange
            var twitchService = Substitute.For<ITwitchService>();
            var logger = Substitute.For<ILogger<UptimeHandler>>();
            var handler = new UptimeHandler(logger, twitchService);

            var startTime = DateTime.UtcNow.AddHours(-3).AddMinutes(-45);
            twitchService.StreamStartedAt().Returns(startTime);

            var uptimeType = new UptimeType();
            var variables = new Dictionary<string, string>();

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
            var logger = Substitute.For<ILogger<UptimeHandler>>();
            var handler = new UptimeHandler(logger, twitchService);

            twitchService.StreamStartedAt().Returns(DateTime.MinValue);

            var uptimeType = new UptimeType();
            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(uptimeType, variables);

            // Assert
            Assert.Equal("Offline", variables["Uptime"]);
        }

        [Fact]
        public async Task WrongType_LogsError()
        {
            // Arrange
            var twitchService = Substitute.For<ITwitchService>();
            var logger = Substitute.For<ILogger<UptimeHandler>>();
            var handler = new UptimeHandler(logger, twitchService);

            var wrongType = new SubActionType();
            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(wrongType, variables);

            // Assert
            logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Invalid sub action type for UptimeHandler")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
