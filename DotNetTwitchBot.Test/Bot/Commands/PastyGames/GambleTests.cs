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
using DotNetTwitchBot.Bot.TwitchServices;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DotNetTwitchBot.Test.Bot.Commands.PastyGames
{
    public class GambleTests
    {
        private readonly ILogger<Gamble> _logger;
        private readonly IGameSettingsService _gameSettingsService;
        private readonly IPointsSystem _pointsSystem;
        private readonly ITwitchService _twitchService;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly ICommandHandler _commandHandler;
        private readonly IMediator mediatorSubstitute;
        private readonly ITools _tools;
        private readonly MaxBetCalculator _maxBetCalculator;
        private readonly Gamble _gamble;

        public GambleTests()
        {
            _logger = Substitute.For<ILogger<Gamble>>();
            _gameSettingsService = Substitute.For<IGameSettingsService>();
            _pointsSystem = Substitute.For<IPointsSystem>();
            _twitchService = Substitute.For<ITwitchService>();
            _serviceBackbone = Substitute.For<IServiceBackbone>();
            _commandHandler = Substitute.For<ICommandHandler>();
            mediatorSubstitute = Substitute.For<IMediator>();
            _tools = Substitute.For<ITools>();
            _maxBetCalculator = new MaxBetCalculator(_pointsSystem);

            _gamble = new Gamble(
                _logger,
                _gameSettingsService,
                _pointsSystem,
                _twitchService,
                _serviceBackbone,
                _commandHandler,
                mediatorSubstitute,
                _tools,
                _maxBetCalculator
            );
        }

        [Fact]
        public async Task Register_ShouldRegisterCommandsAndDefaultPoint()
        {
            await _gamble.Register();

            await _commandHandler.Received(1).AddDefaultCommand(Arg.Is<DefaultCommand>(cmd =>
                cmd.CommandName == "gamble" &&
                cmd.UserCooldown == 180));

            await _commandHandler.Received(1).AddDefaultCommand(Arg.Is<DefaultCommand>(cmd =>
                cmd.CommandName == "jackpot"));

            await _pointsSystem.Received(1).RegisterDefaultPointForGame(Gamble.GAMENAME);
        }

        [Fact]
        public async Task OnCommand_WithJackpotCommand_ShouldDisplayJackpot()
        {
            // Arrange
            _pointsSystem.GetPointTypeForGame(Gamble.GAMENAME).Returns(new PointType { Name = "Points" });
            _commandHandler.GetCommand("jackpot").Returns(new Command(new BaseCommandProperties { CommandName = "jackpot" }, _gamble));
            _gameSettingsService.GetLongSetting(Gamble.GAMENAME, Gamble.CURRENT_JACKPOT, 1000).Returns(5000);
            _gameSettingsService.GetStringSetting(Gamble.GAMENAME, Gamble.CURRENT_JACKPOT_MESSAGE, Arg.Any<string>())
                .Returns("The current jackpot is {jackpot} {PointType}");

            var eventArgs = new CommandEventArgs { Command = "jackpot", DisplayName = "TestUser" };

            // Act
            await _gamble.OnCommand(this, eventArgs);

            // Assert
            await _serviceBackbone.Received(1).ResponseWithMessage(eventArgs, "The current jackpot is 5,000 Points");
        }

        [Fact]
        public async Task HandleGamble_WithNoArgs_ShouldThrowSkipCooldownException()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = "gamble",
                DisplayName = "TestUser",
                Args = new List<string>()
            };

            _gameSettingsService.GetStringSetting(Gamble.GAMENAME, Gamble.INCORRECT_ARGS, Arg.Any<string>())
                .Returns("To gamble, do !{Command} amount");
            _commandHandler.GetCommand("gamble").Returns(new Command(new BaseCommandProperties { CommandName = "gamble" }, _gamble));

            // Act & Assert
            await Assert.ThrowsAsync<SkipCooldownException>(() => _gamble.OnCommand(this, eventArgs));
            await _serviceBackbone.Received(1).ResponseWithMessage(eventArgs, Arg.Any<string>());
        }

        [Fact]
        public async Task HandleGamble_WhenWinningJackpot_ShouldAwardJackpot()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = "gamble",
                DisplayName = "TestUser",
                UserId = "123",
                Args = new List<string> { "100" }
            };

            _serviceBackbone.IsOnline.Returns(true);

            _commandHandler.GetCommand("gamble").Returns(new Command(new BaseCommandProperties { CommandName = "gamble" }, _gamble));

            _pointsSystem.RemovePointsFromUserByUserIdAndGame(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>())
                .Returns(true);

            _gameSettingsService.GetIntSetting(Gamble.GAMENAME, Gamble.JACKPOT_NUMBER, 69).Returns(69);
            _gameSettingsService.GetIntSetting(Gamble.GAMENAME, Gamble.MINIMUM_FOR_WIN, 48).Returns(48);
            _gameSettingsService.GetIntSetting(Gamble.GAMENAME, Gamble.WINNING_MULTIPLIER, 2).Returns(2);
            _gameSettingsService.GetLongSetting(Gamble.GAMENAME, Gamble.CURRENT_JACKPOT, 1000).Returns(5000);
            _gameSettingsService.GetStringSetting(Gamble.GAMENAME, Gamble.JACKPOT_MESSAGE, Arg.Any<string>())
                .Returns("{Name} rolled {Rolled} and won the jackpot of {Points} {PointType}!");

            _tools.Next(1, 101).Returns(69); // Jackpot number

            _pointsSystem.GetPointTypeForGame(Gamble.GAMENAME).Returns(new PointType { Name = "Points" });

            // Act
            await _gamble.OnCommand(this, eventArgs);

            // Assert
            await _twitchService.Received(1).Announcement(Arg.Any<string>());
            await _pointsSystem.Received(1).AddPointsByUserIdAndGame("123", Gamble.GAMENAME, Arg.Any<long>());
        }

        [Fact]
        public async Task HandleGamble_WhenWinngJackpotWhileOffline_ShouldAwardPoints()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = "gamble",
                DisplayName = "TestUser",
                UserId = "123",
                Args = new List<string> { "100" }
            };

            _serviceBackbone.IsOnline.Returns(false);

            _commandHandler.GetCommand("gamble").Returns(new Command(new BaseCommandProperties { CommandName = "gamble" }, _gamble));

            _pointsSystem.RemovePointsFromUserByUserIdAndGame(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>())
                .Returns(true);

            _gameSettingsService.GetIntSetting(Gamble.GAMENAME, Gamble.MINIMUM_FOR_WIN, 48).Returns(48);
            _gameSettingsService.GetIntSetting(Gamble.GAMENAME, Gamble.WINNING_MULTIPLIER, 2).Returns(2);
            _tools.Next(1, 101).Returns(69); // Winning roll

            _pointsSystem.GetPointTypeForGame(Gamble.GAMENAME).Returns(new PointType { Name = "Points" });

            // Act
            await _gamble.OnCommand(this, eventArgs);

            // Assert
            await _twitchService.Received(0).Announcement(Arg.Any<string>());
            await _pointsSystem.Received(1).AddPointsByUserIdAndGame("123", Gamble.GAMENAME, 200);
        }

        [Fact]
        public async Task HandleGamble_WhenWinning_ShouldAwardPoints()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = "gamble",
                DisplayName = "TestUser",
                UserId = "123",
                Args = new List<string> { "100" }
            };

            _commandHandler.GetCommand("gamble").Returns(new Command(new BaseCommandProperties { CommandName = "gamble" }, _gamble));

            _pointsSystem.RemovePointsFromUserByUserIdAndGame(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>())
                .Returns(true);

            _gameSettingsService.GetIntSetting(Gamble.GAMENAME, Gamble.MINIMUM_FOR_WIN, 48).Returns(48);
            _gameSettingsService.GetIntSetting(Gamble.GAMENAME, Gamble.WINNING_MULTIPLIER, 2).Returns(2);
            _tools.Next(1, 101).Returns(50); // Winning roll

            _pointsSystem.GetPointTypeForGame(Gamble.GAMENAME).Returns(new PointType { Name = "Points" });

            // Act
            await _gamble.OnCommand(this, eventArgs);

            // Assert
            await _pointsSystem.Received(1).AddPointsByUserIdAndGame("123", Gamble.GAMENAME, 200);
        }

        [Fact]
        public async Task HandleGamble_WhenLosing_ShouldUpdateJackpot()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = "gamble",
                DisplayName = "TestUser",
                UserId = "123",
                Args = new List<string> { "100" }
            };
            _pointsSystem.RemovePointsFromUserByUserIdAndGame(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>())
                .Returns(true);
            _commandHandler.GetCommand("gamble").Returns(new Command(new BaseCommandProperties { CommandName = "gamble" }, _gamble));

            _gameSettingsService.GetIntSetting(Gamble.GAMENAME, Gamble.MINIMUM_FOR_WIN, 48).Returns(48);
            _gameSettingsService.GetDoubleSetting(Gamble.GAMENAME, Gamble.JACKPOT_CONTRIBUTION, 0.10).Returns(0.10);
            _tools.Next(1, 101).Returns(47); // Losing roll

            _pointsSystem.GetPointTypeForGame(Gamble.GAMENAME).Returns(new PointType { Name = "Points" });

            // Act
            await _gamble.OnCommand(this, eventArgs);

            // Assert
            await _gameSettingsService.Received(1).SetLongSetting(
                Gamble.GAMENAME,
                Gamble.CURRENT_JACKPOT,
                Arg.Is<long>(l => l == 10)); // 10% of 100
        }
    }
}
