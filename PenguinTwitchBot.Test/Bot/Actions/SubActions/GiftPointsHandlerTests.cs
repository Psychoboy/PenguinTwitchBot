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
    public class GiftPointsHandlerTests
    {
        [Fact]
        public async Task ValidType_GiftsPointsSuccessfully()
        {
            var logger = Substitute.For<ILogger<GiftPointsHandler>>();
            var pointsSystem = Substitute.For<IPointsSystem>();
            var viewerFeature = Substitute.For<IViewerFeature>();

            var handler = new GiftPointsHandler(logger, pointsSystem, viewerFeature);

            var pointType = new PointType { Id = 1, Name = "Gold" };
            var fromViewer = new Viewer { UserId = "1", Username = "fromuser" };
            var toViewer = new Viewer { UserId = "2", Username = "touser" };

            pointsSystem.GetPointTypeByName("Gold").Returns(pointType);
            viewerFeature.GetViewerByUserName("fromuser").Returns(fromViewer);
            viewerFeature.GetViewerByUserName("touser").Returns(toViewer);
            pointsSystem.RemovePointsFromUserByUserId("1", 1, 100).Returns(true);

            var type = new GiftPointsType { Text = "Gold", Amount = "100", FromUsername = "fromuser", TargetName = "touser" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            await pointsSystem.Received(1).AddPointsByUserId("2", 1, 100);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var logger = Substitute.For<ILogger<GiftPointsHandler>>();
            var pointsSystem = Substitute.For<IPointsSystem>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var handler = new GiftPointsHandler(logger, pointsSystem, viewerFeature);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task SameUser_ThrowsException()
        {
            var logger = Substitute.For<ILogger<GiftPointsHandler>>();
            var pointsSystem = Substitute.For<IPointsSystem>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var handler = new GiftPointsHandler(logger, pointsSystem, viewerFeature);

            var pointType = new PointType { Id = 1, Name = "Gold" };
            pointsSystem.GetPointTypeByName("Gold").Returns(pointType);

            var type = new GiftPointsType { Text = "Gold", Amount = "100", FromUsername = "sameuser", TargetName = "sameuser" };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
