using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Points;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class CheckPointsHandlerTests
    {
        [Fact]
        public async Task ValidType_SetsTargetPointsVariable()
        {
            var logger = Substitute.For<ILogger<CheckPointsHandler>>();
            var pointsSystem = Substitute.For<IPointsSystem>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            var handler = new CheckPointsHandler(logger, pointsSystem, viewerFeature);

            var pointType = new PointType { Id = 1, Name = "Gold" };
            var viewer = new Viewer { UserId = "123", Username = "testuser" };

            pointsSystem.GetPointTypeByName("Gold").Returns(pointType);
            viewerFeature.GetViewerByUserName("testuser").Returns(viewer);
            pointsSystem.GetUserPointsByUserId("123", 1).Returns(new UserPoints { Points = 500 });

            var type = new CheckPointsType { PointTypeName = "Gold", TargetUser = "testuser" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("500", variables["TargetPoints"]);
            Assert.Equal("500", variables["TargetPointsFormatted"]);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var logger = Substitute.For<ILogger<CheckPointsHandler>>();
            var pointsSystem = Substitute.For<IPointsSystem>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var handler = new CheckPointsHandler(logger, pointsSystem, viewerFeature);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task PointTypeNotFound_ThrowsException()
        {
            var logger = Substitute.For<ILogger<CheckPointsHandler>>();
            var pointsSystem = Substitute.For<IPointsSystem>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var handler = new CheckPointsHandler(logger, pointsSystem, viewerFeature);

            pointsSystem.GetPointTypeByName("Invalid").Returns((PointType?)null);

            var type = new CheckPointsType { PointTypeName = "Invalid", TargetUser = "testuser" };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
