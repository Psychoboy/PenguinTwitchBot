using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class ToggleCommandDisabledHandlerTests
    {
        [Fact]
        public async Task ValidType_TogglesDisabledState()
        {
            var commandService = Substitute.For<IActionCommandService>();
            var handler = new ToggleCommandDisabledHandler(commandService);

            var command = new PenguinTwitchBot.Database.Bot.Models.Commands.ActionCommand { Id = 1, CommandName = "!test", Disabled = false };
            commandService.GetByCommandNameAsync("!test").Returns(command);

            var type = new ToggleCommandDisabledType { CommandName = "!test", IsDisabled = true };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.True(command.Disabled);
            await commandService.Received(1).UpdateAsync(command);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var commandService = Substitute.For<IActionCommandService>();
            var handler = new ToggleCommandDisabledHandler(commandService);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task CommandNotFound_ThrowsException()
        {
            var commandService = Substitute.For<IActionCommandService>();
            var handler = new ToggleCommandDisabledHandler(commandService);

            commandService.GetByCommandNameAsync("!missing").Returns((PenguinTwitchBot.Database.Bot.Models.Commands.ActionCommand?)null);

            var type = new ToggleCommandDisabledType { CommandName = "!missing", IsDisabled = true };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
