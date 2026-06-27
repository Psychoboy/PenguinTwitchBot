using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class ForEachViewerHandlerTests
    {
        [Fact]
        public async Task ValidType_IteratesActiveViewers()
        {
            var actionService = Substitute.For<IActionManagementService>();
            var action = Substitute.For<IAction>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var handler = new ForEachViewerHandler(actionService, action, viewerFeature, serviceBackbone);

            var actionType = new ActionType { Id = 1, Name = "Test Action", QueueName = "default" };
            actionService.GetActionByIdAsync(1).Returns(actionType);
            viewerFeature.GetActiveViewers().Returns(new List<string> { "user1", "user2" });
            serviceBackbone.IsKnownBot(Arg.Any<string>()).Returns(false);

            var type = new ForEachViewerType { ActionId = 1, ViewerScope = "activeviewers" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            await action.Received(2).EnqueueAction(Arg.Any<ConcurrentDictionary<string, string>>(), actionType, Arg.Any<Guid?>(), Arg.Any<int?>());
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var actionService = Substitute.For<IActionManagementService>();
            var action = Substitute.For<IAction>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var handler = new ForEachViewerHandler(actionService, action, viewerFeature, serviceBackbone);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task MissingActionId_ThrowsException()
        {
            var actionService = Substitute.For<IActionManagementService>();
            var action = Substitute.For<IAction>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var handler = new ForEachViewerHandler(actionService, action, viewerFeature, serviceBackbone);

            var type = new ForEachViewerType { ActionId = null };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
