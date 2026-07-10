using System.Text.Json;
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
}