using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using PenguinTwitchBot.Application.Alert.Notification;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Application.TTS;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.TTS;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Commands;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.TTS
{
    public class TTSServiceTests
    {
        private readonly IServiceBackbone _serviceBackbone = Substitute.For<IServiceBackbone>();
        private readonly ICommandHandler _commandHandler = Substitute.For<ICommandHandler>();
        private readonly ILogger<TTSService> _logger = Substitute.For<ILogger<TTSService>>();
        private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
        private readonly IPenguinDispatcher _dispatcher = Substitute.For<IPenguinDispatcher>();
        private readonly IWebHostEnvironment _environment = Substitute.For<IWebHostEnvironment>();
        private readonly ITTSPlayerService _ttsPlayerService = Substitute.For<ITTSPlayerService>();
        private readonly IPiperService _piperService = Substitute.For<IPiperService>();
        private readonly ITTSSettingsService _ttsSettingsService = Substitute.For<ITTSSettingsService>();
        private readonly ITwitchService _twitchService = Substitute.For<ITwitchService>();

        private TTSService CreateSut()
        {
            return new TTSService(
                _serviceBackbone,
                _commandHandler,
                _logger,
                _scopeFactory,
                _dispatcher,
                _environment,
                _ttsPlayerService,
                _piperService,
                _ttsSettingsService,
                _twitchService
            );
        }

        [Fact]
        public async Task SayMessage_WhenMessageRejectedByAutoMod_DoesNotPublishNotification()
        {
            // Arrange
            var sut = CreateSut();
            var voice = new RegisteredVoice { Name = "af_heart", Type = BaseVoice.VoiceType.Kokoro };
            const string badMessage = "bad message";
            _twitchService.WillBePermittedByAutomod(badMessage).Returns(false);

            // Act
            await sut.SayMessage(voice, badMessage);

            // Assert
            await _twitchService.Received(1).WillBePermittedByAutomod(badMessage);
            await _dispatcher.DidNotReceive().Publish(Arg.Any<TTSCreateNotification>());
        }

        [Fact]
        public async Task SayMessage_WhenMessagePermittedByAutoMod_PublishesNotification()
        {
            // Arrange
            var sut = CreateSut();
            var voice = new RegisteredVoice { Name = "af_heart", Type = BaseVoice.VoiceType.Kokoro };
            const string cleanMessage = "Hello world";
            _twitchService.WillBePermittedByAutomod(cleanMessage).Returns(true);

            // Act
            await sut.SayMessage(voice, cleanMessage);

            // Assert
            await _twitchService.Received(1).WillBePermittedByAutomod(cleanMessage);
            await _dispatcher.Received(1).Publish(Arg.Is<TTSCreateNotification>(n =>
                n.TTSRequest.Message == cleanMessage &&
                n.TTSRequest.RegisteredVoice == voice));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SayMessage_WhenMessageIsNullOrWhitespace_DoesNotCallAutoModOrPublish(string? emptyMessage)
        {
            // Arrange
            var sut = CreateSut();
            var voice = new RegisteredVoice { Name = "af_heart", Type = BaseVoice.VoiceType.Kokoro };

            // Act
            await sut.SayMessage(voice, emptyMessage!);

            // Assert
            await _twitchService.DidNotReceive().WillBePermittedByAutomod(Arg.Any<string>());
            await _dispatcher.DidNotReceive().Publish(Arg.Any<TTSCreateNotification>());
        }

        [Fact]
        public async Task OnCommand_WhenSayCommandPermitted_PublishesNotification()
        {
            // Arrange
            var sut = CreateSut();
            var command = new Command(new BaseCommandProperties { CommandName = "say" }, sut);
            _commandHandler.GetCommand("say").Returns(command);

            var scope = Substitute.For<IServiceScope>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var uow = Substitute.For<IUnitOfWork>();

            var userVoices = new List<UserRegisteredVoice> { }.BuildMockDbSet().AsQueryable();
            var registeredVoices = new List<RegisteredVoice>
            {
                new() { Name = "af_heart", Type = BaseVoice.VoiceType.Kokoro }
            }.BuildMockDbSet().AsQueryable();

            uow.UserRegisteredVoices.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<UserRegisteredVoice, bool>>>())
                .Returns(userVoices);
            uow.RegisteredVoices.GetAllAsync().Returns(registeredVoices);

            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(uow);
            scope.ServiceProvider.Returns(serviceProvider);
            _scopeFactory.CreateScope().Returns(scope);

            const string expectedFullMessage = "Psycho says hello world";
            _twitchService.WillBePermittedByAutomod(expectedFullMessage).Returns(true);

            var eventArgs = new CommandEventArgs
            {
                Command = "say",
                Name = "Psycho",
                Arg = "hello world"
            };

            // Act
            await sut.OnCommand(null, eventArgs);

            // Assert
            await _twitchService.Received(1).WillBePermittedByAutomod(expectedFullMessage);
            await _dispatcher.Received(1).Publish(Arg.Is<TTSCreateNotification>(n =>
                n.TTSRequest.Message == expectedFullMessage));
        }

        [Fact]
        public async Task OnCommand_WhenSayCommandRejectedByAutoMod_DoesNotPublishNotification()
        {
            // Arrange
            var sut = CreateSut();
            var command = new Command(new BaseCommandProperties { CommandName = "say" }, sut);
            _commandHandler.GetCommand("say").Returns(command);

            var scope = Substitute.For<IServiceScope>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var uow = Substitute.For<IUnitOfWork>();

            var userVoices = new List<UserRegisteredVoice> { }.BuildMockDbSet().AsQueryable();
            var registeredVoices = new List<RegisteredVoice>
            {
                new() { Name = "af_heart", Type = BaseVoice.VoiceType.Kokoro }
            }.BuildMockDbSet().AsQueryable();

            uow.UserRegisteredVoices.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<UserRegisteredVoice, bool>>>())
                .Returns(userVoices);
            uow.RegisteredVoices.GetAllAsync().Returns(registeredVoices);

            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(uow);
            scope.ServiceProvider.Returns(serviceProvider);
            _scopeFactory.CreateScope().Returns(scope);

            const string expectedFullMessage = "Psycho says bad language";
            _twitchService.WillBePermittedByAutomod(expectedFullMessage).Returns(false);

            var eventArgs = new CommandEventArgs
            {
                Command = "say",
                Name = "Psycho",
                Arg = "bad language"
            };

            // Act
            await sut.OnCommand(null, eventArgs);

            // Assert
            await _twitchService.Received(1).WillBePermittedByAutomod(expectedFullMessage);
            await _dispatcher.DidNotReceive().Publish(Arg.Any<TTSCreateNotification>());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task OnCommand_WhenArgIsNullOrWhitespace_ReturnsEarly(string? emptyArg)
        {
            // Arrange
            var sut = CreateSut();
            var command = new Command(new BaseCommandProperties { CommandName = "say" }, sut);
            _commandHandler.GetCommand("say").Returns(command);

            var eventArgs = new CommandEventArgs
            {
                Command = "say",
                Name = "Psycho",
                Arg = emptyArg!
            };

            // Act
            await sut.OnCommand(null, eventArgs);

            // Assert
            await _twitchService.DidNotReceive().WillBePermittedByAutomod(Arg.Any<string>());
            await _dispatcher.DidNotReceive().Publish(Arg.Any<TTSCreateNotification>());
        }
    }
}
