using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class FollowAgeHandlerTests
    {
        [Fact]
        public async Task ValidFollowAgeType_WithFollower_SetsFollowAgeVariables()
        {
            // Arrange
            var viewerFeature = Substitute.For<IViewerFeature>();
            var logger = Substitute.For<ILogger<FollowAgeType>>();
            var handler = new FollowAgeHandler(logger, viewerFeature);

            var follower = new Follower
            {
                DisplayName = "TestUser",
                FollowDate = DateTime.Now.AddDays(-30)
            };

            viewerFeature.GetFollowerAsync("TestUser").Returns(follower);

            var followAgeType = new FollowAgeType
            {
                Text = "%user%"
            };

            var variables = new Dictionary<string, string> { { "user", "TestUser" } };

            // Act
            await handler.ExecuteAsync(followAgeType, variables);

            // Assert
            Assert.True(variables.ContainsKey("followage"));
            Assert.Contains("TestUser", variables["followage"]);
            Assert.Contains("30 days ago", variables["followage"]);
            Assert.True(variables.ContainsKey("followage_date"));
            await viewerFeature.Received(1).GetFollowerAsync("TestUser");
        }

        [Fact]
        public async Task NotFollower_SetsNotFollowerMessage()
        {
            // Arrange
            var viewerFeature = Substitute.For<IViewerFeature>();
            var logger = Substitute.For<ILogger<FollowAgeType>>();
            var handler = new FollowAgeHandler(logger, viewerFeature);

            viewerFeature.GetFollowerAsync("NotAFollower").Returns((Follower?)null);

            var followAgeType = new FollowAgeType
            {
                Text = "NotAFollower"
            };

            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(followAgeType, variables);

            // Assert
            Assert.Equal("NotAFollower is not a follower", variables["followage"]);
            Assert.Equal("N/A", variables["followage_date"]);
        }

        [Fact]
        public async Task WrongType_LogsError()
        {
            // Arrange
            var viewerFeature = Substitute.For<IViewerFeature>();
            var logger = Substitute.For<ILogger<FollowAgeType>>();
            var handler = new FollowAgeHandler(logger, viewerFeature);

            var wrongType = new SendMessageType();
            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(wrongType, variables);

            // Assert
            logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Invalid sub action type for FollowAgeHandler")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
