using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Actions;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Commands.Actions;

public class ActionCommandHandlerTests
{
    private readonly IActionManagementService _actionManagement;
    private readonly IAction _actionService;
    private readonly IActionCommandService _actionCommandService;
    private readonly ICommandHandler _commandHandler;
    private readonly ILogger<ActionCommandHandler> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ActionCommandHandler _handler;

    public ActionCommandHandlerTests()
    {
        _actionManagement = Substitute.For<IActionManagementService>();
        _actionService = Substitute.For<IAction>();
        _actionCommandService = Substitute.For<IActionCommandService>();
        _commandHandler = Substitute.For<ICommandHandler>();
        _logger = Substitute.For<ILogger<ActionCommandHandler>>();

        var services = new ServiceCollection();
        services.AddSingleton(_actionManagement);
        services.AddSingleton(_actionService);
        services.AddSingleton(_actionCommandService);
        services.AddSingleton(_commandHandler);
        services.AddSingleton(_logger);
        services.AddSingleton(Substitute.For<ILogger<ActionCommandService>>());
        services.AddSingleton(Substitute.For<ILogger<CommandHandler>>());

        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        _handler = new ActionCommandHandler(
            _scopeFactory,
            _commandHandler,
            _logger);
    }

    [Fact]
    public async Task Handle_NullEventArgs_ReturnsEarly()
    {
        var notification = new RunCommandNotification { EventArgs = null };

        await _handler.Handle(notification, CancellationToken.None);

        await _actionCommandService.DidNotReceive().GetByCommandNameAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_EmptyCommand_ReturnsEarly()
    {
        var notification = new RunCommandNotification
        {
            EventArgs = new CommandEventArgs { Command = "" }
        };

        await _handler.Handle(notification, CancellationToken.None);

        await _actionCommandService.DidNotReceive().GetByCommandNameAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_WhitespaceCommand_ReturnsEarly()
    {
        var notification = new RunCommandNotification
        {
            EventArgs = new CommandEventArgs { Command = "   " }
        };

        await _handler.Handle(notification, CancellationToken.None);

        await _actionCommandService.DidNotReceive().GetByCommandNameAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_CommandNotFound_ReturnsEarly()
    {
        var notification = new RunCommandNotification
        {
            EventArgs = new CommandEventArgs { Command = "test" }
        };

        _actionCommandService.GetByCommandNameAsync("test").Returns((ActionCommand?)null);

        await _handler.Handle(notification, CancellationToken.None);

        await _actionManagement.DidNotReceive().GetActionsByTriggerTypeAndNameAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_CommandDisabled_ReturnsEarly()
    {
        var notification = new RunCommandNotification
        {
            EventArgs = new CommandEventArgs { Command = "test" }
        };

        var command = new ActionCommand { CommandName = "test", Disabled = true };
        _actionCommandService.GetByCommandNameAsync("test").Returns(command);

        await _handler.Handle(notification, CancellationToken.None);

        await _actionManagement.DidNotReceive().GetActionsByTriggerTypeAndNameAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_SharedChatRestricted_LogsWarningAndReturns()
    {
        var notification = new RunCommandNotification
        {
            EventArgs = new CommandEventArgs
            {
                Command = "test",
                DisplayName = "SomeUser",
                FromOwnChannel = false
            }
        };

        var command = new ActionCommand
        {
            CommandName = "test",
            Disabled = false,
            SourceOnly = true
        };
        _actionCommandService.GetByCommandNameAsync("test").Returns(command);

        await _handler.Handle(notification, CancellationToken.None);

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("attempted to run broadcaster-only command")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
        await _actionManagement.DidNotReceive().GetActionsByTriggerTypeAndNameAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_SharedChatAllowed_ProceedsPastCheck()
    {
        var notification = new RunCommandNotification
        {
            EventArgs = new CommandEventArgs
            {
                Command = "test",
                DisplayName = "SomeUser",
                FromOwnChannel = true
            }
        };

        var command = new ActionCommand
        {
            CommandName = "test",
            Disabled = false,
            SourceOnly = true
        };
        _actionCommandService.GetByCommandNameAsync("test").Returns(command);
        _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
        _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true));
        _actionManagement.GetActionsByTriggerTypeAndNameAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>()).Returns(Task.FromResult(new List<ActionType>()));

        await _handler.Handle(notification, CancellationToken.None);

        await _commandHandler.Received(1).CheckPermission(command, notification.EventArgs);
        _logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("broadcaster-only")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_PermissionDenied_LogsWarningAndReturns()
    {
        var notification = new RunCommandNotification
        {
            EventArgs = new CommandEventArgs
            {
                Command = "test",
                DisplayName = "SomeUser"
            }
        };

        var command = new ActionCommand
        {
            CommandName = "test",
            Disabled = false,
            SourceOnly = false
        };
        _actionCommandService.GetByCommandNameAsync("test").Returns(command);
        _commandHandler.CheckPermission(command, notification.EventArgs).Returns(Task.FromResult(false));

        await _handler.Handle(notification, CancellationToken.None);

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("does not have permission to run command")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
        await _actionManagement.DidNotReceive().GetActionsByTriggerTypeAndNameAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_SayCooldownActive_ReturnsEarly()
    {
        var notification = new RunCommandNotification
        {
            EventArgs = new CommandEventArgs
            {
                Command = "test",
                Name = "testuser",
                DisplayName = "TestUser"
            }
        };

        var command = new ActionCommand
        {
            CommandName = "test",
            Disabled = false,
            SourceOnly = false,
            SayCooldown = true
        };
        _actionCommandService.GetByCommandNameAsync("test").Returns(command);
        _commandHandler.CheckPermission(command, notification.EventArgs).Returns(Task.FromResult(true));
        _commandHandler.IsGlobalCoolDownExpiredWithMessageForAction(
            "testuser", "TestUser", "test").Returns(Task.FromResult(false));

        await _handler.Handle(notification, CancellationToken.None);

        await _actionManagement.DidNotReceive().GetActionsByTriggerTypeAndNameAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_UserCooldownActive_ReturnsEarly()
    {
        var notification = new RunCommandNotification
        {
            EventArgs = new CommandEventArgs
            {
                Command = "test",
                Name = "testuser",
                DisplayName = "TestUser"
            }
        };

        var command = new ActionCommand
        {
            CommandName = "test",
            Disabled = false,
            SourceOnly = false,
            SayCooldown = false
        };
        _actionCommandService.GetByCommandNameAsync("test").Returns(command);
        _commandHandler.CheckPermission(command, notification.EventArgs).Returns(Task.FromResult(true));
        _commandHandler.IsCoolDownExpired("testuser", "test").Returns(Task.FromResult(false));

        await _handler.Handle(notification, CancellationToken.None);

        await _actionManagement.DidNotReceive().GetActionsByTriggerTypeAndNameAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_Success_EnqueuesActionsAndSetsCooldowns()
    {
        var notification = new RunCommandNotification
        {
            EventArgs = new CommandEventArgs
            {
                Command = "test",
                Name = "testuser",
                DisplayName = "TestUser"
            }
        };

        var command = new ActionCommand
        {
            CommandName = "test",
            Disabled = false,
            SourceOnly = false,
            SayCooldown = false,
            GlobalCooldown = 10,
            GlobalCooldownMax = 20,
            UserCooldown = 5,
            UserCooldownMax = 10
        };
        var actionType1 = new ActionType { Name = "Action1", SubActions = new List<SubActionType>() };
        var actionType2 = new ActionType { Name = "Action2", SubActions = new List<SubActionType>() };

        _actionCommandService.GetByCommandNameAsync("test").Returns(command);
        _commandHandler.CheckPermission(command, notification.EventArgs).Returns(Task.FromResult(true));
        _commandHandler.IsCoolDownExpired("testuser", "test").Returns(Task.FromResult(true));
        _actionManagement.GetActionsByTriggerTypeAndNameAsync(
            TriggerTypes.Command, "!test").Returns(Task.FromResult(new List<ActionType> { actionType1, actionType2 }));

        await _handler.Handle(notification, CancellationToken.None);

        await _actionService.Received(1).EnqueueAction(Arg.Any<ConcurrentDictionary<string, string>>(), actionType1);
        await _actionService.Received(1).EnqueueAction(Arg.Any<ConcurrentDictionary<string, string>>(), actionType2);
        await _commandHandler.Received(1).AddGlobalCooldown("test", Arg.Is<int>(v => v >= 10 && v <= 20));
        await _commandHandler.Received(1).AddCoolDown("testuser", "test", Arg.Is<int>(v => v >= 5 && v <= 10));
    }

    [Fact]
    public async Task Handle_Exception_LogsErrorAndReleasesLock()
    {
        var notification = new RunCommandNotification
        {
            EventArgs = new CommandEventArgs
            {
                Command = "test",
                Name = "testuser",
                DisplayName = "TestUser"
            }
        };

        var command = new ActionCommand { CommandName = "test", Disabled = false, SourceOnly = false };
        _actionCommandService.GetByCommandNameAsync("test").Returns(command);
        _commandHandler.CheckPermission(command, notification.EventArgs).Returns(Task.FromResult(true));
        _commandHandler.IsCoolDownExpired("testuser", "test").Returns(Task.FromResult(true));
        _actionManagement.GetActionsByTriggerTypeAndNameAsync(
            TriggerTypes.Command, "!test").Returns(Task.FromResult(new List<ActionType>()));

        _actionService.EnqueueAction(Arg.Any<ConcurrentDictionary<string, string>>(), Arg.Any<ActionType>())
            .Returns(Task.FromException(new Exception("Test exception")));

        var exception = await Record.ExceptionAsync(() => _handler.Handle(notification, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Handle_Success_NoCooldownsConfigured_DoesNotCallAddCooldown()
    {
        var notification = new RunCommandNotification
        {
            EventArgs = new CommandEventArgs
            {
                Command = "test",
                Name = "testuser",
                DisplayName = "TestUser"
            }
        };

        var command = new ActionCommand
        {
            CommandName = "test",
            Disabled = false,
            SourceOnly = false,
            SayCooldown = false,
            GlobalCooldown = 0,
            UserCooldown = 0
        };
        var actionType = new ActionType { Name = "Action1", SubActions = new List<SubActionType>() };

        _actionCommandService.GetByCommandNameAsync("test").Returns(command);
        _commandHandler.CheckPermission(command, notification.EventArgs).Returns(Task.FromResult(true));
        _commandHandler.IsCoolDownExpired("testuser", "test").Returns(Task.FromResult(true));
        _actionManagement.GetActionsByTriggerTypeAndNameAsync(
            TriggerTypes.Command, "!test").Returns(Task.FromResult(new List<ActionType> { actionType }));

        await _handler.Handle(notification, CancellationToken.None);

        await _commandHandler.DidNotReceive().AddGlobalCooldown(Arg.Any<string>(), Arg.Any<int>());
        await _commandHandler.DidNotReceive().AddCoolDown(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }
}
