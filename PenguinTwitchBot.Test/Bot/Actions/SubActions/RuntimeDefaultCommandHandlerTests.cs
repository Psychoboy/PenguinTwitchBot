using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using NSubstitute;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class RuntimeDefaultCommandHandlerTests
    {
        private readonly ICommandHandler _commandHandler;
        private readonly ILogger<RuntimeDefaultCommandHandler> _logger;
        private readonly RuntimeDefaultCommandHandler _handler;

        public RuntimeDefaultCommandHandlerTests()
        {
            _commandHandler = Substitute.For<ICommandHandler>();
            _logger = Substitute.For<ILogger<RuntimeDefaultCommandHandler>>();
            _handler = new RuntimeDefaultCommandHandler(_commandHandler, _logger);
        }

        private static ConcurrentDictionary<string, string> CreateVariables(string command = "!test", bool skipLock = true, CommandEventArgs? eventArgs = null)
        {
            var args = eventArgs ?? new CommandEventArgs { Command = command, Name = "testuser", DisplayName = "TestUser" };
            return new ConcurrentDictionary<string, string>
            {
                ["command"] = command,
                ["IsMod"] = "false",
                ["IsBroadcaster"] = "true",
                ["IsSub"] = "false",
                ["IsVip"] = "false",
                ["Args"] = "",
                ["TargetUser"] = "",
                ["SkipLock"] = skipLock.ToString(),
                ["OriginalEventArgs"] = JsonSerializer.Serialize(args)
            };
        }

        [Fact]
        public async Task ValidType_ExecutesRuntimeCommand()
        {
            var baseCommandService = Substitute.For<IBaseCommandService>();
            var command = new Command(
                new BaseCommandProperties { CommandName = "!test", Disabled = false },
                baseCommandService);
            _commandHandler.GetCommand("!test").Returns(command);
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(true);
            _commandHandler.IsCoolDownExpiredWithMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BaseCommandProperties>()).Returns(true);
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
            baseCommandService.OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>()).Returns(Task.CompletedTask);

            var variables = CreateVariables();
            var type = new RuntimeDefaultCommandType();
            await _handler.ExecuteAsync(type, variables, null, -1);

            _commandHandler.Received(1).GetCommand("!test");
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => _handler.ExecuteAsync(wrongType, variables, null, -1));
        }

        [Fact]
        public async Task InvalidVariables_ThrowsException()
        {
            var type = new RuntimeDefaultCommandType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => _handler.ExecuteAsync(type, variables, null, -1));
        }

        [Fact]
        public async Task CommandNotFound_ReturnsWithoutError()
        {
            _commandHandler.GetCommand("!missing").Returns((Command?)null);

            var variables = CreateVariables("!missing");
            var type = new RuntimeDefaultCommandType();
            await _handler.ExecuteAsync(type, variables, null, -1);

            _commandHandler.Received(1).GetCommand("!missing");
        }

        [Fact]
        public async Task CommandDisabled_ReturnsWithoutError()
        {
            var command = new Command(
                new BaseCommandProperties { CommandName = "!test", Disabled = true },
                Substitute.For<IBaseCommandService>());
            _commandHandler.GetCommand("!test").Returns(command);

            var variables = CreateVariables();
            var type = new RuntimeDefaultCommandType();
            await _handler.ExecuteAsync(type, variables, null, -1);

            _commandHandler.Received(1).GetCommand("!test");
        }

        [Fact]
        public async Task NotAllowedInSharedChat_ReturnsWithoutError()
        {
            var baseCommandService = Substitute.For<IBaseCommandService>();
            var command = new Command(
                new BaseCommandProperties { CommandName = "!test", Disabled = false, SourceOnly = true },
                baseCommandService);
            _commandHandler.GetCommand("!test").Returns(command);

            var args = new CommandEventArgs { Command = "!test", Name = "testuser", DisplayName = "TestUser", FromOwnChannel = false };
            var variables = CreateVariables(eventArgs: args);
            var type = new RuntimeDefaultCommandType();
            await _handler.ExecuteAsync(type, variables, null, -1);

            _commandHandler.Received(1).GetCommand("!test");
        }

        [Fact]
        public async Task CheckPermissionFalse_ReturnsWithoutError()
        {
            var baseCommandService = Substitute.For<IBaseCommandService>();
            var command = new Command(
                new BaseCommandProperties { CommandName = "!test", Disabled = false },
                baseCommandService);
            _commandHandler.GetCommand("!test").Returns(command);
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(false);

            var variables = CreateVariables();
            var type = new RuntimeDefaultCommandType();
            await _handler.ExecuteAsync(type, variables, null, -1);

            await _commandHandler.Received(1).CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>());
        }

        [Fact]
        public async Task SayCooldownExpired_ExecutesCommand()
        {
            var baseCommandService = Substitute.For<IBaseCommandService>();
            var command = new Command(
                new BaseCommandProperties { CommandName = "!test", Disabled = false, SayCooldown = true },
                baseCommandService);
            _commandHandler.GetCommand("!test").Returns(command);
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(true);
            _commandHandler.IsCoolDownExpiredWithMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BaseCommandProperties>()).Returns(true);
            baseCommandService.OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>()).Returns(Task.CompletedTask);

            var variables = CreateVariables();
            var type = new RuntimeDefaultCommandType();
            await _handler.ExecuteAsync(type, variables, null, -1);

            await baseCommandService.Received(1).OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>());
        }

        [Fact]
        public async Task SayCooldownNotExpired_ReturnsWithoutExecuting()
        {
            var baseCommandService = Substitute.For<IBaseCommandService>();
            var command = new Command(
                new BaseCommandProperties { CommandName = "!test", Disabled = false, SayCooldown = true },
                baseCommandService);
            _commandHandler.GetCommand("!test").Returns(command);
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(true);
            _commandHandler.IsCoolDownExpiredWithMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BaseCommandProperties>()).Returns(false);

            var variables = CreateVariables();
            var type = new RuntimeDefaultCommandType();
            await _handler.ExecuteAsync(type, variables, null, -1);

            await baseCommandService.DidNotReceive().OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>());
        }

        [Fact]
        public async Task NoSayCooldownExpired_ExecutesCommand()
        {
            var baseCommandService = Substitute.For<IBaseCommandService>();
            var command = new Command(
                new BaseCommandProperties { CommandName = "!test", Disabled = false, SayCooldown = false },
                baseCommandService);
            _commandHandler.GetCommand("!test").Returns(command);
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(true);
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
            baseCommandService.OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>()).Returns(Task.CompletedTask);

            var variables = CreateVariables();
            var type = new RuntimeDefaultCommandType();
            await _handler.ExecuteAsync(type, variables, null, -1);

            await baseCommandService.Received(1).OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>());
        }

        [Fact]
        public async Task NoSayCooldownNotExpired_ReturnsWithoutExecuting()
        {
            var baseCommandService = Substitute.For<IBaseCommandService>();
            var command = new Command(
                new BaseCommandProperties { CommandName = "!test", Disabled = false, SayCooldown = false },
                baseCommandService);
            _commandHandler.GetCommand("!test").Returns(command);
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(true);
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

            var variables = CreateVariables();
            var type = new RuntimeDefaultCommandType();
            await _handler.ExecuteAsync(type, variables, null, -1);

            await baseCommandService.DidNotReceive().OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>());
        }

        [Fact]
        public async Task GlobalCooldown_SetAfterExecution()
        {
            var baseCommandService = Substitute.For<IBaseCommandService>();
            var command = new Command(
                new BaseCommandProperties { CommandName = "!test", Disabled = false, SayCooldown = false, GlobalCooldown = 5, GlobalCooldownMax = 10 },
                baseCommandService);
            _commandHandler.GetCommand("!test").Returns(command);
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(true);
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
            baseCommandService.OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>()).Returns(Task.CompletedTask);

            var variables = CreateVariables();
            var type = new RuntimeDefaultCommandType();
            await _handler.ExecuteAsync(type, variables, null, -1);

            await _commandHandler.Received(1).AddGlobalCooldown("!test", Arg.Any<int>());
        }

        [Fact]
        public async Task UserCooldown_SetAfterExecution()
        {
            var baseCommandService = Substitute.For<IBaseCommandService>();
            var command = new Command(
                new BaseCommandProperties { CommandName = "!test", Disabled = false, SayCooldown = false, UserCooldown = 5, UserCooldownMax = 10 },
                baseCommandService);
            _commandHandler.GetCommand("!test").Returns(command);
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(true);
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
            baseCommandService.OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>()).Returns(Task.CompletedTask);

            var variables = CreateVariables();
            var type = new RuntimeDefaultCommandType();
            await _handler.ExecuteAsync(type, variables, null, -1);

            await _commandHandler.Received(1).AddCoolDown("testuser", "!test", Arg.Any<int>());
        }

        [Fact]
        public async Task SkipCooldownException_IgnoredAndContinues()
        {
            var baseCommandService = Substitute.For<IBaseCommandService>();
            var command = new Command(
                new BaseCommandProperties { CommandName = "!test", Disabled = false, SayCooldown = false, GlobalCooldown = 5 },
                baseCommandService);
            _commandHandler.GetCommand("!test").Returns(command);
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(true);
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
            baseCommandService.OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>())
                .Returns(Task.FromException(new SkipCooldownException("test")));

            var variables = CreateVariables();
            var type = new RuntimeDefaultCommandType();
            await _handler.ExecuteAsync(type, variables, null, -1);

            await _commandHandler.DidNotReceive().AddGlobalCooldown("!test", Arg.Any<int>());
        }

        [Fact]
        public async Task SkipLockFalse_AcquiresAndReleasesLock()
        {
            var baseCommandService = Substitute.For<IBaseCommandService>();
            var command = new Command(
                new BaseCommandProperties { CommandName = "!test", Disabled = false, SayCooldown = false },
                baseCommandService);
            _commandHandler.GetCommand("!test").Returns(command);
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(true);
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
            baseCommandService.OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>()).Returns(Task.CompletedTask);

            var variables = CreateVariables(skipLock: false);
            var type = new RuntimeDefaultCommandType();
            await _handler.ExecuteAsync(type, variables, null, -1);

            await baseCommandService.Received(1).OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>());
        }

        [Fact]
        public async Task SkipLockFalse_LockTimeout_ContinuesExecution()
        {
            var baseCommandService = Substitute.For<IBaseCommandService>();
            var command = new Command(
                new BaseCommandProperties { CommandName = "!test-timeout", Disabled = false, SayCooldown = false },
                baseCommandService);
            _commandHandler.GetCommand("!test-timeout").Returns(command);
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(true);
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
            baseCommandService.OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>()).Returns(Task.CompletedTask);

            var variables = CreateVariables("!test-timeout", skipLock: false);
            var type = new RuntimeDefaultCommandType();

            var lockObj = new SemaphoreSlim(0);
            var commandLockField = typeof(RuntimeDefaultCommandHandler).GetField("CommandLock", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            var dict = (ConcurrentDictionary<string, SemaphoreSlim>)commandLockField.GetValue(null)!;
            dict.GetOrAdd("!test-timeout", _ => lockObj);

            try
            {
                await _handler.ExecuteAsync(type, variables, null, -1);
            }
            finally
            {
                dict.TryRemove("!test-timeout", out _);
                lockObj.Release();
            }

            await baseCommandService.Received(1).OnCommand(Arg.Any<object>(), Arg.Any<CommandEventArgs>());
        }

        [Fact]
        public async Task SupportedType_ReturnsRuntimeDefaultCommand()
        {
            Assert.Equal(SubActionTypes.RuntimeDefaultCommand, _handler.SupportedType);
        }
    }
}
