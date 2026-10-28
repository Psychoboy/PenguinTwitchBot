using System.Text.Json;
using Bunit;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Bot.Actions.Triggers;
using PenguinTwitchBot.Bot.Actions.Variables.Triggers;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Pages.Components;

namespace PenguinTwitchBot.Test.Bot.Commands;

public sealed class DefaultCommandTriggerServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;
    private readonly IAction _actionService;
    private readonly IFeatureRuntimeCoordinator _featureRuntimeCoordinator;
    private readonly IServiceProvider _serviceProvider;

    public DefaultCommandTriggerServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(dbOptions);
        _context.Database.EnsureCreated();

        _actionService = Substitute.For<IAction>();
        _featureRuntimeCoordinator = Substitute.For<IFeatureRuntimeCoordinator>();
        _featureRuntimeCoordinator.GetFeatures().Returns([
            new RuntimeFeatureState(
                FeatureKeys.WheeledGame,
                "Wheeled Game",
                "WheelService",
                false,
                false,
                false,
                "Wheel feature")
        ]);

        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(_actionService);
        services.AddSingleton(_featureRuntimeCoordinator);

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        (_serviceProvider as IDisposable)?.Dispose();
    }

    [Fact]
    public async Task TriggerDefaultCommandEventAsync_ExecutesWizardStyleNestedTrigger()
    {
        var defaultCommand = new DefaultCommand { CommandName = "plinko", CustomCommandName = "plinko", ModuleName = "DropGames" };
        _context.DefaultCommands.Add(defaultCommand);
        await _context.SaveChangesAsync();
        var action = new ActionType
        {
            Name = "Plinko Bounce Plinko - Win", Enabled = true, QueueName = "Default",
            Triggers =
            [
                new()
                {
                    Name = "Plinko Bounce Plinko - Win", Type = TriggerTypes.DefaultCommand, Enabled = true,
                    DefaultCommandId = defaultCommand.Id,
                    Configuration = JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
                    {
                        DefaultCommandName = "plinko", EventType = DefaultCommandEventTypes.PlinkoWin
                    })
                }
            ]
        };
        using (var scope = _serviceProvider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Actions.CreateActionAsync(action);

        var service = new DefaultCommandTriggerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<ILogger<DefaultCommandTriggerService>>(), _featureRuntimeCoordinator);
        await service.TriggerDefaultCommandEventAsync("plinko", DefaultCommandEventTypes.PlinkoWin,
            new CommandEventArgs { Command = "plinko", DisplayName = "Pilot", Name = "pilot" });

        await _actionService.Received(1).EnqueueAction(Arg.Any<System.Collections.Concurrent.ConcurrentDictionary<string, string>>(),
            Arg.Is<ActionType>(queued => queued.Id == action.Id));
    }

    public static IEnumerable<object[]> MinigameOutcomes() => TriggerCatalog.AllTriggers
        .Where(item => item.Type == TriggerTypes.DefaultCommand &&
            item.DefaultCommandName is "drop" or "plinko" or "balloon" or "curl" or "golf" or "starship")
        .SelectMany(item => new[] { false, true }.Select(existingAction =>
            new object[] { item.DefaultCommandName!, item.DefaultCommandEventType!, existingAction }));

    [Theory]
    [InlineData("plinko_Win", "Plinko - Win")]
    [InlineData("My special Plinko reward", "My special Plinko reward")]
    public void TriggerDisplayName_UsesReadableLabelOnlyForLegacyGeneratedNames(string savedName, string expectedName)
    {
        var trigger = new TriggerType
        {
            Name = savedName, Type = TriggerTypes.DefaultCommand,
            Configuration = JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
            {
                DefaultCommandName = "plinko", EventType = DefaultCommandEventTypes.PlinkoWin
            })
        };

        Assert.Equal(expectedName, TriggerCatalog.GetDefaultCommandTriggerDisplayName(trigger));
        Assert.Equal(savedName, trigger.Name);
        trigger.Configuration = "{";
        Assert.Equal(savedName, TriggerCatalog.GetDefaultCommandTriggerDisplayName(trigger));
    }

    [Fact]
    public void DefaultCommandNames_UseCatalogLabelsAcrossAllBuiltInCategories()
    {
        foreach (var item in TriggerCatalog.AllTriggers.Where(item => item.Type == TriggerTypes.DefaultCommand))
        {
            Assert.Equal(item.DisplayName, TriggerCatalog.GetDefaultCommandTriggerName(item.DefaultCommandName!, item.DefaultCommandEventType!));
            var trigger = new TriggerType
            {
                Type = item.Type,
                Name = $"{item.DefaultCommandName}_{item.DefaultCommandEventType!.Split('.').Last()}",
                Configuration = JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
                {
                    DefaultCommandName = item.DefaultCommandName!, EventType = item.DefaultCommandEventType!
                })
            };
            Assert.Equal(item.DisplayName, TriggerCatalog.GetDefaultCommandTriggerDisplayName(trigger));
        }
    }

    [Theory]
    [MemberData(nameof(MinigameOutcomes))]
    public async Task AutomationWizard_AllMinigameOutcomesPersistCanonicalTriggersAndDispatch(string commandName, string eventType, bool existingAction)
    {
        var defaultCommand = new DefaultCommand
        {
            CommandName = commandName, CustomCommandName = "custom-" + commandName, ModuleName = "DropGames"
        };
        var action = new ActionType { Name = "Existing action", Enabled = true };
        _context.DefaultCommands.Add(defaultCommand);
        if (existingAction) _context.Actions.Add(action);
        await _context.SaveChangesAsync();

        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton<IActionManagementService>(new ActionManagementService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(), Substitute.For<ILogger<ActionManagementService>>()));
        var commands = Substitute.For<ICommandHandler>();
        commands.GetDefaultCommandByDefaultCommandName(commandName).Returns(defaultCommand);
        context.Services.AddSingleton(commands);
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypes().Returns(Task.FromResult<IEnumerable<PointType>>([new() { Id = Guid.CreateVersion7(), Name = "Credits" }]));
        context.Services.AddSingleton(points);
        context.Services.AddSingleton<ITriggerVariableResolver, DefaultCommandTriggerVariableResolver>();
        var provider = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        await context.Renderer.Dispatcher.InvokeAsync(() => dialogs.ShowAsync<GameOutcomeAutomationWizard>("Automation",
            new DialogParameters<GameOutcomeAutomationWizard>
            {
                { component => component.DefaultCommandName, commandName },
                { component => component.GameTitle, "Game" },
                { component => component.InitialEventType, eventType }
            }));
        var wizard = provider.FindComponent<GameOutcomeAutomationWizard>();
        if (existingAction)
        {
            var toggle = wizard.FindComponents<MudSwitch<bool>>().Single(component => component.Instance.Label == "Create a new action");
            await wizard.InvokeAsync(() => toggle.Instance.ValueChanged.InvokeAsync(false));
            var select = wizard.FindComponent<MudSelect<Guid?>>();
            await wizard.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(action.Id));
        }
        else
        {
            var message = wizard.FindComponents<MudSwitch<bool>>().Single(component => component.Instance.Label == "Add an outcome chat message");
            await wizard.InvokeAsync(() => message.Instance.ValueChanged.InvokeAsync(true));
        }

        await wizard.FindAll("button").Single(button => button.TextContent.Trim() == "Create Link").ClickAsync(new MouseEventArgs());

        var trigger = await _context.Triggers.AsNoTracking().Include(item => item.Action).ThenInclude(item => item!.SubActions).SingleAsync();
        Assert.Equal(TriggerCatalog.FindItem(TriggerTypes.DefaultCommand, commandName, eventType)!.DisplayName, trigger.Name);
        Assert.Equal(defaultCommand.Id, trigger.DefaultCommandId);
        Assert.True(trigger.Enabled);
        var configuration = JsonSerializer.Deserialize<DefaultCommandTriggerConfiguration>(trigger.Configuration)!;
        Assert.Equal(commandName, configuration.DefaultCommandName);
        Assert.Equal(eventType, configuration.EventType);
        if (!existingAction)
        {
            Assert.NotEmpty(trigger.Action!.SubActions);
            Assert.All(trigger.Action.SubActions, subAction => Assert.True(subAction.Enabled));
            Assert.Equal(Enumerable.Range(0, trigger.Action.SubActions.Count), trigger.Action.SubActions.Select(item => item.Index).Order());
        }
        var service = new DefaultCommandTriggerService(_serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<ILogger<DefaultCommandTriggerService>>(), _featureRuntimeCoordinator);
        await service.TriggerDefaultCommandEventAsync(commandName, eventType,
            new CommandEventArgs { Command = commandName, UserId = "pilot-id", DisplayName = "Pilot", Name = "pilot" },
            new Dictionary<string, string> { ["Score"] = "100", ["ChatMessage"] = "A full event message" });

        await _actionService.Received(1).EnqueueAction(
            Arg.Is<System.Collections.Concurrent.ConcurrentDictionary<string, string>>(variables => variables["UserId"] == "pilot-id" && variables["Score"] == "100"),
            Arg.Is<ActionType>(queued => queued.Id == trigger.ActionId));

        if (existingAction && commandName == "plinko" && eventType == DefaultCommandEventTypes.PlinkoWin)
        {
            await context.Renderer.Dispatcher.InvokeAsync(() => dialogs.ShowAsync<GameOutcomeAutomationWizard>("Repeat setup",
                new DialogParameters<GameOutcomeAutomationWizard>
                {
                    { component => component.DefaultCommandName, commandName },
                    { component => component.GameTitle, "Game" },
                    { component => component.InitialEventType, eventType }
                }));
            var repeated = provider.FindComponent<GameOutcomeAutomationWizard>();
            var toggle = repeated.FindComponents<MudSwitch<bool>>().Single(component => component.Instance.Label == "Create a new action");
            await repeated.InvokeAsync(() => toggle.Instance.ValueChanged.InvokeAsync(false));
            await repeated.InvokeAsync(() => repeated.FindComponent<MudSelect<Guid?>>().Instance.ValueChanged.InvokeAsync(action.Id));
            await repeated.FindAll("button").Single(button => button.TextContent.Trim() == "Create Link").ClickAsync(new MouseEventArgs());
            Assert.Contains("already linked", repeated.Markup);
            Assert.Equal(1, await _context.Triggers.CountAsync());

            trigger.Enabled = false;
            await context.Services.GetRequiredService<IActionManagementService>().UpdateTriggerAsync(trigger);
            await repeated.FindAll("button").Single(button => button.TextContent.Trim() == "Create Link").ClickAsync(new MouseEventArgs());
            var reenabled = await _context.Triggers.AsNoTracking().SingleAsync();
            Assert.Equal(trigger.Id, reenabled.Id);
            Assert.True(reenabled.Enabled);
        }
    }

    [Fact]
    public async Task TriggerDefaultCommandEventAsync_DoesNotExecuteActions_WhenFeatureDisabled()
    {
        var defaultCommand = new DefaultCommand
        {
            CommandName = "spinwheel",
            CustomCommandName = "spinwheel",
            ModuleName = "WheelService"
        };

        _context.DefaultCommands.Add(defaultCommand);

        var action = new ActionType
        {
            Name = "Wheel Action",
            Enabled = true
        };
        _context.Actions.Add(action);

        await _context.SaveChangesAsync();

        _context.Triggers.Add(new TriggerType
        {
            Name = "Wheel Spin Result",
            Type = TriggerTypes.DefaultCommand,
            Enabled = true,
            Configuration = JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
            {
                DefaultCommandName = "spinwheel",
                EventType = DefaultCommandEventTypes.WheelSpinResult
            }),
            ActionId = action.Id,
            DefaultCommandId = defaultCommand.Id
        });

        await _context.SaveChangesAsync();

        var service = new DefaultCommandTriggerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<ILogger<DefaultCommandTriggerService>>(),
            _featureRuntimeCoordinator);

        await service.TriggerDefaultCommandEventAsync(
            "spinwheel",
            DefaultCommandEventTypes.WheelSpinResult,
            new CommandEventArgs
            {
                Command = "spinwheel",
                DisplayName = "TestUser",
                Name = "testuser"
            },
            new Dictionary<string, string>
            {
                ["WinningLabel"] = "Prize"
            });

        await _actionService.DidNotReceiveWithAnyArgs().EnqueueAction(default!, default!);
    }

    [Fact]
    public async Task TriggerDefaultCommandEventAsync_RacingPlacementFilter_ExecutesOnlyForMatchingPlacement()
    {
        var raceCommand = new DefaultCommand
        {
            CommandName = "race",
            CustomCommandName = "race",
            ModuleName = "Racing"
        };
        _context.DefaultCommands.Add(raceCommand);

        var firstPlaceAction = new ActionType { Name = "First Place Action", Enabled = true };
        _context.Actions.Add(firstPlaceAction);
        await _context.SaveChangesAsync();

        _context.Triggers.Add(new TriggerType
        {
            Name = "Racing 1st Place",
            Type = TriggerTypes.DefaultCommand,
            Enabled = true,
            Configuration = JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
            {
                DefaultCommandName = "race",
                EventType = DefaultCommandEventTypes.RacingEnd,
                Placements = [1]
            }),
            ActionId = firstPlaceAction.Id,
            DefaultCommandId = raceCommand.Id
        });
        await _context.SaveChangesAsync();

        var service = new DefaultCommandTriggerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<ILogger<DefaultCommandTriggerService>>(),
            _featureRuntimeCoordinator);

        var eventArgs = new CommandEventArgs { Command = "race", DisplayName = "Winner", Name = "winner" };

        // Test 2nd place - should NOT fire
        await service.TriggerDefaultCommandEventAsync("race", DefaultCommandEventTypes.RacingEnd, eventArgs, new Dictionary<string, string>
        {
            ["Placement"] = "2",
            ["RacingMode"] = "GrandPrix"
        });
        await _actionService.DidNotReceiveWithAnyArgs().EnqueueAction(default!, default!);

        // Test 1st place - SHOULD fire
        await service.TriggerDefaultCommandEventAsync("race", DefaultCommandEventTypes.RacingEnd, eventArgs, new Dictionary<string, string>
        {
            ["Placement"] = "1",
            ["RacingMode"] = "GrandPrix"
        });
        await _actionService.Received(1).EnqueueAction(
            Arg.Is<System.Collections.Concurrent.ConcurrentDictionary<string, string>>(dict => dict["Placement"] == "1"),
            Arg.Is<ActionType>(a => a.Id == firstPlaceAction.Id));
    }

    [Fact]
    public void DefaultCommandTriggerConfiguration_MatchesVariables_ValidatesAllRacingFilters()
    {
        var config = new DefaultCommandTriggerConfiguration
        {
            DefaultCommandName = "race",
            EventType = DefaultCommandEventTypes.RacingEnd,
            Placements = [1, 2, 3],
            RacingGameModes = ["DestructionDerby"],
            RacingOutcomeFilter = "Survived",
            MinKnockouts = 2
        };

        // Complete match
        var matchingVars = new Dictionary<string, string>
        {
            ["Placement"] = "1",
            ["RacingMode"] = "DestructionDerby",
            ["IsTotaled"] = "False",
            ["KnockoutCount"] = "3"
        };
        Assert.True(config.MatchesVariables(matchingVars));

        // Wrong placement
        var wrongPlace = new Dictionary<string, string>(matchingVars) { ["Placement"] = "4" };
        Assert.False(config.MatchesVariables(wrongPlace));

        // Wrong game mode
        var wrongMode = new Dictionary<string, string>(matchingVars) { ["RacingMode"] = "GrandPrix" };
        Assert.False(config.MatchesVariables(wrongMode));

        // Totaled when requiring Survived
        var totaled = new Dictionary<string, string>(matchingVars) { ["IsTotaled"] = "True" };
        Assert.False(config.MatchesVariables(totaled));

        // Not enough knockouts
        var lowKOs = new Dictionary<string, string>(matchingVars) { ["KnockoutCount"] = "1" };
        Assert.False(config.MatchesVariables(lowKOs));
    }
}