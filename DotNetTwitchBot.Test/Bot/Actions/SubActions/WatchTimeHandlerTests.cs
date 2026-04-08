using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands.Features;
using NSubstitute;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class WatchTimeHandlerTests
    {
        [Fact]
        public async Task ValidWatchTimeType_SetsWatchTimeVariable()
        {
            // Arrange
            var loyaltyFeature = Substitute.For<ILoyaltyFeature>();
            var handler = new WatchTimeHandler(loyaltyFeature);

            loyaltyFeature.GetViewerWatchTime("TestUser").Returns("10 hours 30 minutes");

            var watchTimeType = new WatchTimeType
            {
                Text = "TestUser"
            };

            var variables = new ConcurrentDictionary<string, string>();

            // Act
            await handler.ExecuteAsync(watchTimeType, variables);

            // Assert
            Assert.True(variables.ContainsKey("watch_time"));
            Assert.Equal("10 hours 30 minutes", variables["watch_time"]);
            await loyaltyFeature.Received(1).GetViewerWatchTime("TestUser");
        }

        [Fact]
        public async Task WatchTimeType_WithVariableReplacement_ReplacesVariables()
        {
            // Arrange
            var loyaltyFeature = Substitute.For<ILoyaltyFeature>();
            var handler = new WatchTimeHandler(loyaltyFeature);

            loyaltyFeature.GetViewerWatchTime("TestUser").Returns("5 hours");

            var watchTimeType = new WatchTimeType
            {
                Text = "%user%"
            };

            var variables = new ConcurrentDictionary<string, string> { ["user"] = "TestUser" };

            // Act
            await handler.ExecuteAsync(watchTimeType, variables);

            // Assert
            Assert.Equal("5 hours", variables["watch_time"]);
            await loyaltyFeature.Received(1).GetViewerWatchTime("TestUser");
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var loyaltyFeature = Substitute.For<ILoyaltyFeature>();
            var handler = new WatchTimeHandler(loyaltyFeature);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => handler.ExecuteAsync(wrongType, variables));
        }
    }
}
