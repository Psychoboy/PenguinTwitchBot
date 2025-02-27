using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Commands.PastyGames;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Commands;
using DotNetTwitchBot.Bot.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Security.Cryptography;
using DotNetTwitchBot.Bot;
namespace DotNetTwitchBot.Tests.Bot.Commands.PastyGames
{
    public class DefuseTests
    {
        private readonly IPointsSystem _pointsSystem;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly IGameSettingsService _gameSettingsService;
        private readonly IViewerFeature _viewerFeature;
        private readonly IMediator _mediator;
        private readonly ILogger<Defuse> _logger;
        private readonly ICommandHandler _commandHandler;
        private readonly RandomNumberGenerator _randomNumberGenerator;
        private readonly Defuse _defuse;

        public DefuseTests()
        {
            _pointsSystem = Substitute.For<IPointsSystem>();
            _serviceBackbone = Substitute.For<IServiceBackbone>();
            _gameSettingsService = Substitute.For<IGameSettingsService>();
            _viewerFeature = Substitute.For<IViewerFeature>();
            _mediator = Substitute.For<IMediator>();
            _logger = Substitute.For<ILogger<Defuse>>();
            _commandHandler = Substitute.For<ICommandHandler>();
            _randomNumberGenerator = Substitute.For<RandomNumberGenerator>();

            _pointsSystem.GetPointTypeForGame("Defuse").Returns(new DotNetTwitchBot.Bot.Models.Points.PointType { Name = "Points"});

            _defuse = new Defuse(_pointsSystem, _serviceBackbone, _gameSettingsService, _viewerFeature, _mediator, _logger, _commandHandler);
        }

        [Fact]
        public async Task Register_ShouldRegisterDefaultCommand()
        {
            // Act
            await _defuse.Register();

            // Assert
            await _commandHandler.Received(1).AddDefaultCommand(Arg.Any<DefaultCommand>());
            await _pointsSystem.Received(1).RegisterDefaultPointForGame(Defuse.GAMENAME);
        }

        [Fact]
        public async Task OnCommand_ShouldSendNoArgsMessage_WhenNoArgsProvided()
        {
            // Arrange
            var commandEventArgs = new CommandEventArgs
            {
                Command = "defuse",
                Arg = "",
                Name = "testuser",
                DisplayName = "TestUser"
            };
            _commandHandler.GetCommand("defuse").Returns(new Command(new BaseCommandProperties { CommandName = "defuse" }, _defuse));
            _gameSettingsService.GetStringSetting(Defuse.GAMENAME, Defuse.NO_ARGS, Arg.Any<string>()).Returns("you need to choose one of these wires to cut: {Wires}");
            _gameSettingsService.GetStringListSetting(Defuse.GAMENAME, Defuse.WIRES, Arg.Any<List<string>>()).Returns(Task.FromResult(new List<string> { "red", "blue", "yellow" }));

            // Act
            await Assert.ThrowsAsync<SkipCooldownException>(() => _defuse.OnCommand(null, commandEventArgs));

            // Assert
            await _serviceBackbone.Received(1).SendChatMessage("TestUser", "you need to choose one of these wires to cut: red, blue, yellow");
        }

        [Fact]
        public async Task OnCommand_ShouldSendNotEnoughMessage_WhenUserHasInsufficientPoints()
        {
            // Arrange
            var commandEventArgs = new CommandEventArgs
            {
                Command = "defuse",
                Arg = "red",
                Name = "testuser",
                DisplayName = "TestUser",
                UserId = "123"
            };
            _commandHandler.GetCommand("defuse").Returns(new Command(new BaseCommandProperties { CommandName = "defuse" }, _defuse));
            _gameSettingsService.GetStringSetting(Defuse.GAMENAME, Defuse.NOT_ENOUGH, Arg.Any<string>()).Returns("Sorry it costs {Cost} {PointType} to defuse the bomb which you do not have.");
            _gameSettingsService.GetIntSetting(Defuse.GAMENAME, Defuse.COST, Arg.Any<int>()).Returns(Task.FromResult(500));
            _pointsSystem.RemovePointsFromUserByUserIdAndGame("123", Defuse.GAMENAME, 500).Returns(Task.FromResult(false));
            _gameSettingsService.GetStringListSetting(Defuse.GAMENAME, Defuse.WIRES, Arg.Any<List<string>>()).Returns(Task.FromResult(new List<string> { "red", "blue", "yellow" }));

            // Act
            await Assert.ThrowsAsync<SkipCooldownException>(() => _defuse.OnCommand(null, commandEventArgs));

            // Assert
            await _serviceBackbone.Received(1).SendChatMessage("TestUser", "Sorry it costs 500 Points to defuse the bomb which you do not have.");
        }

        [Fact]
        public async Task OnCommand_ShouldSendSuccessMessage_WhenUserChoosesCorrectWire()
        {
            // Arrange
            var commandEventArgs = new CommandEventArgs
            {
                Command = "defuse",
                Arg = "red",
                Name = "testuser",
                DisplayName = "TestUser",
                UserId = "123"
            };

            _commandHandler.GetCommand("defuse").Returns(new Command(new BaseCommandProperties { CommandName = "defuse" }, _defuse));
            _gameSettingsService.GetStringSetting(Defuse.GAMENAME, Defuse.STARTING, Arg.Any<string>()).Returns("The bomb is beeping and {Name} cuts the {Wire} wire... ");
            _gameSettingsService.GetStringSetting(Defuse.GAMENAME, Defuse.SUCCESS, Arg.Any<string>()).Returns("The bomb goes silent. As a thank for saving the day you got awarded {Points} {PointType}");
            _gameSettingsService.GetIntSetting(Defuse.GAMENAME, Defuse.COST, Arg.Any<int>()).Returns(Task.FromResult(500));
            _pointsSystem.RemovePointsFromUserByUserIdAndGame("123", Defuse.GAMENAME, 500).Returns(Task.FromResult(true));
            _pointsSystem.AddPointsByUserIdAndGame("123", Defuse.GAMENAME, Arg.Any<long>()).Returns(Task.FromResult(1500L));
            _gameSettingsService.GetStringListSetting(Defuse.GAMENAME, Defuse.WIRES, Arg.Any<List<string>>()).Returns(Task.FromResult(new List<string> { "red", "blue", "green"}));
            _viewerFeature.GetNameWithTitle("testuser").Returns(Task.FromResult("TestUser"));

            // Act
            await _defuse.OnCommand(null, commandEventArgs);

            // Assert
            await _serviceBackbone.Received(1).SendChatMessage(Arg.Is<string>(x => x.StartsWith("The bomb is beeping and TestUser cuts the red wire... The bomb goes silent.")));
            await _mediator.Received(1).Publish(Arg.Any<QueueAlert>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task OnCommand_ShouldSendFailureMessage_WhenUserChoosesIncorrectWire()
        {
            // Arrange
            var commandEventArgs = new CommandEventArgs
            {
                Command = "defuse",
                Arg = "red",
                Name = "testuser",
                DisplayName = "TestUser",
                UserId = "123"
            };

            var mockTools = Substitute.For<ITools>();
            _defuse.Tools = mockTools;
            mockTools.Next(0, 2).Returns(1);

            _commandHandler.GetCommand("defuse").Returns(new Command(new BaseCommandProperties { CommandName = "defuse" }, _defuse));
            _gameSettingsService.GetStringSetting(Defuse.GAMENAME, Defuse.STARTING, Arg.Any<string>()).Returns("The bomb is beeping and {Name} cuts the {Wire} wire... ");
            _gameSettingsService.GetStringSetting(Defuse.GAMENAME, Defuse.FAIL, Arg.Any<string>()).Returns("BOOM!!! The bomb explodes, you lose {Points} {PointType}.");
            _gameSettingsService.GetIntSetting(Defuse.GAMENAME, Defuse.COST, Arg.Any<int>()).Returns(Task.FromResult(500));
            _pointsSystem.RemovePointsFromUserByUserIdAndGame("123", Defuse.GAMENAME, 500).Returns(Task.FromResult(true));
            _gameSettingsService.GetStringListSetting(Defuse.GAMENAME, Defuse.WIRES, Arg.Any<List<string>>()).Returns(Task.FromResult(new List<string> {"blue","red","yellow"}));
            _viewerFeature.GetNameWithTitle("testuser").Returns(Task.FromResult("TestUser"));

            // Act
            await _defuse.RunGame(commandEventArgs, ["blue"], 500);

            // Assert
            await _serviceBackbone.Received(1).SendChatMessage("The bomb is beeping and TestUser cuts the red wire... BOOM!!! The bomb explodes, you lose 500 Points.");
            await _mediator.Received(1).Publish(Arg.Any<QueueAlert>(), Arg.Any<CancellationToken>());
        }
    }
}