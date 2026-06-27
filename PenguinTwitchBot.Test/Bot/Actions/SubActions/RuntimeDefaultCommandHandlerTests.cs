using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using NSubstitute;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class RuntimeDefaultCommandHandlerTests
    {
        [Fact]
        public async Task ValidType_ExecutesRuntimeCommand()
        {
            var commandHandler = Substitute.For<ICommandHandler>();
            var logger = Substitute.For<ILogger<RuntimeDefaultCommandHandler>>();
            var handler = new RuntimeDefaultCommandHandler(commandHandler, logger);

            var command = new Command(
                new BaseCommandProperties { CommandName = "!test", Disabled = false },
                Substitute.For<IBaseCommandService>());
            commandHandler.GetCommand("!test").Returns(command);

            var variables = new ConcurrentDictionary<string, string>
            {
                ["command"] = "!test",
                ["IsMod"] = "false",
                ["IsBroadcaster"] = "true",
                ["IsSub"] = "false",
                ["IsVip"] = "false",
                ["Args"] = "",
                ["TargetUser"] = "",
                ["SkipLock"] = "true",
                ["OriginalEventArgs"] = JsonSerializer.Serialize(new CommandEventArgs { Command = "!test", Name = "testuser", DisplayName = "TestUser" })
            };

            var type = new RuntimeDefaultCommandType();
            await handler.ExecuteAsync(type, variables);

            commandHandler.Received(1).GetCommand("!test");
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var commandHandler = Substitute.For<ICommandHandler>();
            var logger = Substitute.For<ILogger<RuntimeDefaultCommandHandler>>();
            var handler = new RuntimeDefaultCommandHandler(commandHandler, logger);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task CommandNotFound_ReturnsWithoutError()
        {
            var commandHandler = Substitute.For<ICommandHandler>();
            var logger = Substitute.For<ILogger<RuntimeDefaultCommandHandler>>();
            var handler = new RuntimeDefaultCommandHandler(commandHandler, logger);

            commandHandler.GetCommand("!missing").Returns((Command?)null);

            var variables = new ConcurrentDictionary<string, string>
            {
                ["command"] = "!missing",
                ["IsMod"] = "false",
                ["IsBroadcaster"] = "true",
                ["IsSub"] = "false",
                ["IsVip"] = "false",
                ["Args"] = "",
                ["TargetUser"] = "",
                ["SkipLock"] = "true",
                ["OriginalEventArgs"] = JsonSerializer.Serialize(new CommandEventArgs { Command = "!missing", Name = "testuser", DisplayName = "TestUser" })
            };

            var type = new RuntimeDefaultCommandType();
            await handler.ExecuteAsync(type, variables);

            commandHandler.Received(1).GetCommand("!missing");
        }
    }
}
