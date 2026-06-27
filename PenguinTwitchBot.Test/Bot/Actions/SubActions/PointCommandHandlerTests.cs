using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class PointCommandHandlerTests
    {
        [Fact]
        public async Task ValidType_RunsPointCommand()
        {
            var pointsSystem = Substitute.For<IPointsSystem>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var handler = new PointCommandHandler(pointsSystem, serviceBackbone);

            var pointCommand = new PenguinTwitchBot.Database.Bot.Models.Points.PointCommand { CommandName = "!reward" };
            pointsSystem.GetPointCommand("!reward").Returns(pointCommand);
            serviceBackbone.BroadcasterName.Returns("broadcaster");

            var type = new PointCommandType { Text = "!reward" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            await serviceBackbone.Received(1).RunCommand(Arg.Is<CommandEventArgs>(e => e.Command == "!reward"));
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var pointsSystem = Substitute.For<IPointsSystem>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var handler = new PointCommandHandler(pointsSystem, serviceBackbone);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task CommandNotFound_ThrowsException()
        {
            var pointsSystem = Substitute.For<IPointsSystem>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var handler = new PointCommandHandler(pointsSystem, serviceBackbone);

            pointsSystem.GetPointCommand("!missing").Returns((PenguinTwitchBot.Database.Bot.Models.Points.PointCommand?)null);

            var type = new PointCommandType { Text = "!missing" };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
