using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Models;
using NSubstitute;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class FollowAgeHandlerTests
    {
        [Fact]
        public async Task ValidFollowAgeType_WithFollower_SetsFollowAgeVariables()
        {
            // Arrange
            var viewerFeature = Substitute.For<IViewerFeature>();
            var handler = new FollowAgeHandler(viewerFeature);

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

            var variables = new ConcurrentDictionary<string, string> { ["user"] = "TestUser" };

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
            var handler = new FollowAgeHandler(viewerFeature);

            viewerFeature.GetFollowerAsync("NotAFollower").Returns((Follower?)null);

            var followAgeType = new FollowAgeType
            {
                Text = "NotAFollower"
            };

            var variables = new ConcurrentDictionary<string, string>();

            // Act
            await handler.ExecuteAsync(followAgeType, variables);

            // Assert
            Assert.Equal("NotAFollower is not a follower", variables["followage"]);
            Assert.Equal("N/A", variables["followage_date"]);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var viewerFeature = Substitute.For<IViewerFeature>();
            var handler = new FollowAgeHandler(viewerFeature);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => handler.ExecuteAsync(wrongType, variables));
        }
    }
}
