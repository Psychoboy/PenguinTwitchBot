using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class ExecuteActionHandlerTests
    {
        [Fact]
        public async Task ValidType_EnqueuesAction()
        {
            var actionService = Substitute.For<IActionManagementService>();
            var action = Substitute.For<IAction>();
            var handler = new ExecuteActionHandler(actionService, action);

            var actionType = new ActionType { Id = 1, Name = "Test Action", QueueName = "default" };
            actionService.GetActionByIdAsync(1).Returns(actionType);

            var type = new ExecuteActionType { ActionId = 1 };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            await action.Received(1).EnqueueAction(Arg.Any<ConcurrentDictionary<string, string>>(), actionType);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var actionService = Substitute.For<IActionManagementService>();
            var action = Substitute.For<IAction>();
            var handler = new ExecuteActionHandler(actionService, action);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task InvalidActionId_ThrowsException()
        {
            var actionService = Substitute.For<IActionManagementService>();
            var action = Substitute.For<IAction>();
            var handler = new ExecuteActionHandler(actionService, action);

            var type = new ExecuteActionType { ActionId = 0 };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
