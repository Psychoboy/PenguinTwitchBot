using DotNetTwitchBot.Bot;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Commands.PastyGames;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Bot.Models.Commands;
using DotNetTwitchBot.Bot.Models.Points;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.Core;
using TwitchLib.Api.Helix.Models.Charity;
using Xunit;

namespace DotNetTwitchBot.Test.Bot.Commands.PastyGames
{
    public class HeistTests
    {
        private readonly IPointsSystem _pointsSystem;
        private readonly IGameSettingsService _gameSettingsService;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly ILogger<Heist> _logger;
        private readonly ICommandHandler _commandHandler;
        private readonly FakeTimeProvider _timeProvider;
        private readonly Heist _heist;
        private readonly ITools _tools;
        private readonly IMediator mediatorSubstitute;

        public HeistTests()
        {
            _pointsSystem = Substitute.For<IPointsSystem>();
            _gameSettingsService = Substitute.For<IGameSettingsService>();
            _serviceBackbone = Substitute.For<IServiceBackbone>();
            _logger = Substitute.For<ILogger<Heist>>();
            _commandHandler = Substitute.For<ICommandHandler>();
            _timeProvider = new FakeTimeProvider();
            _tools = Substitute.For<ITools>();
            mediatorSubstitute = Substitute.For<IMediator>();

            _heist = new Heist(
                _pointsSystem,
                _gameSettingsService,
                _serviceBackbone,
                _logger,
                _commandHandler,
                _timeProvider,
                mediatorSubstitute,
                _tools
            );
        }

        [Fact]
        public async Task Register_ShouldRegisterCommandAndDefaultPoint()
        {
            await _heist.Register();

            await _commandHandler.Received(1).AddDefaultCommand(Arg.Is<DefaultCommand>(cmd =>
                cmd.CommandName == "heist"));
            await _pointsSystem.Received(1).RegisterDefaultPointForGame(Heist.GAMENAME);
        }

        [Fact]
        public async Task OnCommand_WhenGameFinishing_ShouldThrowSkipCooldownException()
        {
            // Arrange
            _commandHandler.GetCommand("heist").Returns(new Command(new BaseCommandProperties { CommandName = "heist" }, _heist));
            _gameSettingsService.GetStringSetting(Heist.GAMENAME, Heist.GAMEFINISHING, Arg.Any<string>())
                .Returns("you can not join the heist now.");

            // Set game state to Finishing using reflection
            var gameStateField = typeof(Heist).GetField("GameState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            gameStateField?.SetValue(_heist, Heist.State.Finishing);

            var eventArgs = new CommandEventArgs { Command = "heist", DisplayName = "TestUser" };

            // Act & Assert
            await Assert.ThrowsAsync<SkipCooldownException>(() =>
                _heist.OnCommand(this, eventArgs));
            await _serviceBackbone.Received(1).ResponseWithMessage(eventArgs, "you can not join the heist now.");
        }

        [Fact]
        public async Task OnCommand_WithNoArgs_ShouldThrowSkipCooldownException()
        {
            // Arrange
            _commandHandler.GetCommand("heist").Returns(new Command(new BaseCommandProperties { CommandName = "heist" }, _heist));
            _gameSettingsService.GetStringSetting(Heist.GAMENAME, Heist.INVALIDARGS, Arg.Any<string>())
                .Returns("To Enter/Start a heist do !{Command} AMOUNT/ALL/MAX/%");

            var eventArgs = new CommandEventArgs
            {
                Command = "heist",
                DisplayName = "TestUser",
                Args = new List<string>()
            };

            // Act & Assert
            await Assert.ThrowsAsync<SkipCooldownException>(() =>
                _heist.OnCommand(this, eventArgs));
        }

        [Fact]
        public async Task OnCommand_WithValidAllBet_ShouldJoinHeist()
        {
            // Arrange
            _commandHandler.GetCommand("heist").Returns(new Command(new BaseCommandProperties { CommandName = "heist" }, _heist));
            _pointsSystem.GetMaxPointsByUserIdAndGame("user123", Heist.GAMENAME, PointsSystem.MaxBet)
                .Returns(1000L);
            _pointsSystem.GetPointTypeForGame(Heist.GAMENAME)
                .Returns(new PointType { Name = "Points" });
            _gameSettingsService.GetIntSetting(Heist.GAMENAME, Heist.MINBET, 10)
                .Returns(10);
            _pointsSystem.RemovePointsFromUserByUserIdAndGame("user123", Heist.GAMENAME, 1000L)
                .Returns(true);

            _gameSettingsService.GetStringSetting(Heist.GAMENAME, Heist.STAGEONE, Arg.Any<string>())
                .Returns("Stage One");
            _gameSettingsService.GetStringSetting(Heist.GAMENAME, Heist.STAGETWO, Arg.Any<string>())
                .Returns("Stage Two");
            _gameSettingsService.GetStringSetting(Heist.GAMENAME, Heist.STAGETHREE, Arg.Any<string>())
                .Returns("Stage Three");
            _gameSettingsService.GetStringSetting(Heist.GAMENAME, Heist.STAGEFOUR, Arg.Any<string>())
                .Returns("{Survivors} managed to sneak past Charlie sptvCharlie and grab some of those precious pasties!");
            _tools.RandomRange(1, 100).Returns(50, 1);

            var eventArgs = new CommandEventArgs
            {
                Command = "heist",
                DisplayName = "TestUser",
                UserId = "user123",
                Name = "testuser",
                Args = new List<string> { "all" }
            };

            // Act
            await _heist.OnCommand(this, eventArgs);

            // Assert
            await _serviceBackbone.Received(5).SendChatMessage(Arg.Any<string>());
        }

        [Theory]
        [InlineData("50%")]
        [InlineData("100")]
        public async Task OnCommand_WithValidBet_ShouldStartHeist(string betAmount)
        {
            // Arrange
            _commandHandler.GetCommand("heist").Returns(new Command(new BaseCommandProperties { CommandName = "heist" }, _heist));
            _pointsSystem.GetMaxPointsByUserIdAndGame(Arg.Any<string>(), Heist.GAMENAME, PointsSystem.MaxBet)
                .Returns(1000L);
            _pointsSystem.GetPointTypeForGame(Heist.GAMENAME)
                .Returns(new PointType { Name = "Points" });
            _gameSettingsService.GetIntSetting(Heist.GAMENAME, Heist.MINBET, 10)
                .Returns(10);
            _pointsSystem.RemovePointsFromUserByUserIdAndGame(Arg.Any<string>(), Heist.GAMENAME, Arg.Any<long>())
                .Returns(true);

            var eventArgs = new CommandEventArgs
            {
                Command = "heist",
                DisplayName = "TestUser",
                UserId = "user123",
                Name = "testuser",
                Args = new List<string> { betAmount }
            };

            // Act
            await _heist.OnCommand(this, eventArgs);

            // Assert
            await _serviceBackbone.Received(5).SendChatMessage(Arg.Any<string>());
        }

        [Fact]
        public async Task OnCommand_WithInsufficientPoints_ShouldThrowSkipCooldownException()
        {
            // Arrange
            _commandHandler.GetCommand("heist").Returns(new Command(new BaseCommandProperties { CommandName = "heist" }, _heist));
            _pointsSystem.GetPointTypeForGame(Heist.GAMENAME)
                .Returns(new PointType { Name = "Points" });
            _gameSettingsService.GetIntSetting(Heist.GAMENAME, Heist.MINBET, 10)
                .Returns(10);
            _pointsSystem.RemovePointsFromUserByUserIdAndGame(Arg.Any<string>(), Heist.GAMENAME, Arg.Any<long>())
                .Returns(false);

            var eventArgs = new CommandEventArgs
            {
                Command = "heist",
                DisplayName = "TestUser",
                UserId = "user123",
                Args = new List<string> { "100" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<SkipCooldownException>(() =>
                _heist.OnCommand(this, eventArgs));
        }

        [Fact]
        public async Task RunStory_ShouldCompleteCycle()
        {
            // Arrange
            _commandHandler.GetCommand("heist").Returns(new Command(new BaseCommandProperties { CommandName = "heist" }, _heist));

            // Add a participant
            var eventArgs = new CommandEventArgs
            {
                Command = "heist",
                DisplayName = "TestUser",
                UserId = "user123",
                Name = "testuser",
                Args = new List<string> { "100" }
            };

            _pointsSystem.GetPointTypeForGame(Heist.GAMENAME)
                .Returns(new PointType { Name = "Points" });
            _gameSettingsService.GetIntSetting(Heist.GAMENAME, Heist.MINBET, 10)
                .Returns(10);
            _pointsSystem.GetMaxPointsByUserIdAndGame(Arg.Any<string>(), Heist.GAMENAME, PointsSystem.MaxBet)
                .Returns(1000L);
            _pointsSystem.RemovePointsFromUserByUserIdAndGame(Arg.Any<string>(), Heist.GAMENAME, Arg.Any<long>())
                .Returns(true);

            _gameSettingsService.GetStringSetting(Heist.GAMENAME, Heist.GAMESTARTING, Arg.Any<string>())
                .Returns("TestUser is trying to get a team together for some serious heist business! use \"!heist AMOUNT/ALL/MAX\" to join!");
            _gameSettingsService.GetStringSetting(Heist.GAMENAME, Heist.SURVIVORS, Arg.Any<string>())
                .Returns("The heist ended! Survivors are: {Payouts}.");
            _gameSettingsService.GetStringSetting(Heist.GAMENAME, Heist.STAGEONE, Arg.Any<string>())
                .Returns("Stage One");
            _gameSettingsService.GetStringSetting(Heist.GAMENAME, Heist.STAGETWO, Arg.Any<string>())
                .Returns("Stage Two");
            _gameSettingsService.GetStringSetting(Heist.GAMENAME, Heist.STAGETHREE, Arg.Any<string>())
                .Returns("Stage Three");
            _gameSettingsService.GetStringSetting(Heist.GAMENAME, Heist.STAGEFOUR, Arg.Any<string>())
                .Returns("{Survivors} managed to sneak past Charlie sptvCharlie and grab some of those precious pasties!");
            _tools.RandomRange(1, 100).Returns(50, 1);

            //Act
            await _heist.OnCommand(this, eventArgs);
            _timeProvider.Advance(TimeSpan.FromSeconds(300));

            // Assert
            await _serviceBackbone.Received(1).SendChatMessage("TestUser is trying to get a team together for some serious heist business! use \"!heist AMOUNT/ALL/MAX\" to join!");
            await _serviceBackbone.Received(1).SendChatMessage("Stage One");
            await _serviceBackbone.Received(1).SendChatMessage("Stage Two");
            await _serviceBackbone.Received(1).SendChatMessage("TestUser managed to sneak past Charlie sptvCharlie and grab some of those precious pasties!");
            await _serviceBackbone.Received(1).SendChatMessage("The heist ended! Survivors are: TestUser (100).");
        }
    }
}
