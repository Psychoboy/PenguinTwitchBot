using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class ExecuteDefaultCommandHandlerTests
    {
        [Fact]
        public async Task ValidType_ExecutesDefaultCommand()
        {
            var commandHandler = Substitute.For<ICommandHandler>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var handler = new ExecuteDefaultCommandHandler(commandHandler, serviceBackbone);

            var defaultCommand = new DefaultCommand { Id = 1, CommandName = "testcmd", CustomCommandName = "!test" };
            commandHandler.GetDefaultCommandByDefaultCommandName("testcmd").Returns(defaultCommand);
            serviceBackbone.BroadcasterName.Returns("broadcaster");

            var type = new ExecuteDefaultCommandType { CommandName = "testcmd" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            await serviceBackbone.Received(1).RunCommand(Arg.Is<CommandEventArgs>(e => e.Command == "!test"));
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var commandHandler = Substitute.For<ICommandHandler>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var handler = new ExecuteDefaultCommandHandler(commandHandler, serviceBackbone);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task CommandNotFound_ThrowsException()
        {
            var commandHandler = Substitute.For<ICommandHandler>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var handler = new ExecuteDefaultCommandHandler(commandHandler, serviceBackbone);

            commandHandler.GetDefaultCommandByDefaultCommandName("missing").Returns((DefaultCommand?)null);

            var type = new ExecuteDefaultCommandType { CommandName = "missing" };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
