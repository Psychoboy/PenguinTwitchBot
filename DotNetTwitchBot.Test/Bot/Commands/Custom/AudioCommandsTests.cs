using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Custom;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Bot.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace DotNetTwitchBot.Tests
{
    public class AudioCommandsTests
    {
        private readonly ILanguage _langage;

        public AudioCommandsTests()
        {
            _langage = new Language();
            _langage.LoadLanguage();
        }
        [Fact]
        public async Task AddAudioCommand_ShouldAddCommandToDatabase()
        {
            // Arrange
            var sendAlerts = Substitute.For<ISendAlerts>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();

            scopeFactory.CreateScope().Returns(scope);

            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<AudioCommand> { }.AsQueryable().BuildMockDbSet();
            dbContext.AudioCommands.Find(x => true).ReturnsForAnyArgs(queryable);

            var audioCommands = new AudioCommands(sendAlerts, viewerFeature, scopeFactory,
                Substitute.For<ILogger<AudioCommands>>(), Substitute.For<IServiceBackbone>(),
                _langage, Substitute.For<ICommandHandler>());


            var audioCommand = new AudioCommand { CommandName = "testCommand" };
            // Act
            await audioCommands.AddAudioCommand(audioCommand);

            // Assert
            await dbContext.AudioCommands.Received(1).AddAsync(audioCommand);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task AddAudioCommand_ShouldNotAddCommandToDatabase()
        {
            // Arrange
            var sendAlerts = Substitute.For<ISendAlerts>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();

            scopeFactory.CreateScope().Returns(scope);

            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);
            var audioCommand = new AudioCommand { CommandName = "testCommand" };
            var queryable = new List<AudioCommand> { audioCommand }.AsQueryable().BuildMockDbSet();
            dbContext.AudioCommands.Find(x => true).ReturnsForAnyArgs(queryable);

            var audioCommands = new AudioCommands(sendAlerts, viewerFeature, scopeFactory,
                Substitute.For<ILogger<AudioCommands>>(), Substitute.For<IServiceBackbone>(),
                _langage, Substitute.For<ICommandHandler>());

            // Act
            await audioCommands.AddAudioCommand(audioCommand);

            // Assert
            await dbContext.AudioCommands.Received(0).AddAsync(audioCommand);
            await dbContext.Received(0).SaveChangesAsync();
        }

        [Fact]
        public async Task SaveAudioCommand_ShouldSave()
        {
            // Arrange
            var sendAlerts = Substitute.For<ISendAlerts>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();

            scopeFactory.CreateScope().Returns(scope);

            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<AudioCommand> { }.AsQueryable().BuildMockDbSet();
            dbContext.AudioCommands.Find(x => true).ReturnsForAnyArgs(queryable);

            var audioCommands = new AudioCommands(sendAlerts, viewerFeature, scopeFactory,
                Substitute.For<ILogger<AudioCommands>>(), Substitute.For<IServiceBackbone>(),
                _langage, Substitute.For<ICommandHandler>());


            var audioCommand = new AudioCommand { CommandName = "testCommand" };
            // Act
            await audioCommands.SaveAudioCommand(audioCommand);

            // Assert
            dbContext.AudioCommands.Received(1).Update(audioCommand);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GetAudioCommand_ShouldGet()
        {
            // Arrange
            var sendAlerts = Substitute.For<ISendAlerts>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();

            scopeFactory.CreateScope().Returns(scope);

            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var audiCommand = new AudioCommand { Id = 1, CommandName = "TestCommand" };
            var queryable = new List<AudioCommand> { audiCommand }.AsQueryable().BuildMockDbSet();
            dbContext.AudioCommands.Find(x => true).ReturnsForAnyArgs(queryable);

            var audioCommands = new AudioCommands(sendAlerts, viewerFeature, scopeFactory,
                Substitute.For<ILogger<AudioCommands>>(), Substitute.For<IServiceBackbone>(),
                _langage, Substitute.For<ICommandHandler>());


            // Act
            var testAudioCommand = await audioCommands.GetAudioCommand(1);

            // Assert
            Assert.Equal(audiCommand, testAudioCommand);
        }

        [Fact]
        public async Task RunCommand_ShouldFailForNotFollower()
        {
            // Arrange
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var sendAlerts = Substitute.For<ISendAlerts>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();

            scopeFactory.CreateScope().Returns(scope);

            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<AudioCommand> { }.AsQueryable().BuildMockDbSet();
            dbContext.AudioCommands.Find(x => true).ReturnsForAnyArgs(queryable);


            var audioCommands = new AudioCommands(sendAlerts, viewerFeature, scopeFactory,
                Substitute.For<ILogger<AudioCommands>>(), serviceBackbone,
                _langage, commandHandler);


            var audioCommand = new AudioCommand { CommandName = "testCommand", Disabled = false, MinimumRank = Rank.Follower };
            var testResult = new List<AudioCommand> { audioCommand };
            dbContext.AudioCommands.GetAllAsync().Returns(testResult);
            await audioCommands.AddAudioCommand(audioCommand);

            commandHandler.IsCoolDownExpiredWithMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            viewerFeature.IsFollower(Arg.Any<string>()).Returns(false);

            // Act

            await audioCommands.RunCommand(new() { Command = "testCommand" });

            // Assert
            await serviceBackbone.Received(1).SendChatMessage(Arg.Any<string>(), "you must be a follower to use that command");
        }

        [Fact]
        public async Task RunCommand_ShouldFailForNotSubscriber()
        {
            // Arrange
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var sendAlerts = Substitute.For<ISendAlerts>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();

            scopeFactory.CreateScope().Returns(scope);

            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<AudioCommand> { }.AsQueryable().BuildMockDbSet();
            dbContext.AudioCommands.Find(x => true).ReturnsForAnyArgs(queryable);


            var audioCommands = new AudioCommands(sendAlerts, viewerFeature, scopeFactory,
                Substitute.For<ILogger<AudioCommands>>(), serviceBackbone,
                _langage, commandHandler);


            var audioCommand = new AudioCommand { CommandName = "testCommand", Disabled = false, MinimumRank = Rank.Subscriber };
            viewerFeature.IsSubscriber(Arg.Any<string>()).Returns(false);
            var testResult = new List<AudioCommand> { audioCommand };
            dbContext.AudioCommands.GetAllAsync().Returns(testResult);
            await audioCommands.AddAudioCommand(audioCommand);

            commandHandler.IsCoolDownExpiredWithMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            // Act
            await audioCommands.RunCommand(new() { Command = "testCommand" });

            // Assert
            await serviceBackbone.Received(1).SendChatMessage(Arg.Any<string>(), "you must be a subscriber to use that command");
        }

        [Fact]
        public async Task RunCommand_ShouldFailForNotModerator()
        {
            // Arrange
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var sendAlerts = Substitute.For<ISendAlerts>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();

            scopeFactory.CreateScope().Returns(scope);

            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<AudioCommand> { }.AsQueryable().BuildMockDbSet();
            dbContext.AudioCommands.Find(x => true).ReturnsForAnyArgs(queryable);


            var audioCommands = new AudioCommands(sendAlerts, viewerFeature, scopeFactory,
                Substitute.For<ILogger<AudioCommands>>(), serviceBackbone,
                _langage, commandHandler);


            var audioCommand = new AudioCommand { CommandName = "testCommand", Disabled = false, MinimumRank = Rank.Moderator };
            viewerFeature.IsModerator(Arg.Any<string>()).Returns(false);
            var testResult = new List<AudioCommand> { audioCommand };
            dbContext.AudioCommands.GetAllAsync().Returns(testResult);
            await audioCommands.AddAudioCommand(audioCommand);

            commandHandler.IsCoolDownExpiredWithMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            // Act
            await audioCommands.RunCommand(new() { Command = "testCommand" });

            // Assert
            await serviceBackbone.Received(1).SendChatMessage(Arg.Any<string>(), "only moderators can do that...");
        }

        [Fact]
        public async Task RunCommand_ShouldFailForNoStreamer()
        {
            // Arrange
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var sendAlerts = Substitute.For<ISendAlerts>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();

            scopeFactory.CreateScope().Returns(scope);

            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<AudioCommand> { }.AsQueryable().BuildMockDbSet();
            dbContext.AudioCommands.Find(x => true).ReturnsForAnyArgs(queryable);


            var audioCommands = new AudioCommands(sendAlerts, viewerFeature, scopeFactory,
                Substitute.For<ILogger<AudioCommands>>(), serviceBackbone,
                _langage, commandHandler);


            var audioCommand = new AudioCommand { CommandName = "testCommand", Disabled = false, MinimumRank = Rank.Streamer };
            serviceBackbone.IsBroadcasterOrBot(Arg.Any<string>()).Returns(false);
            var testResult = new List<AudioCommand> { audioCommand };
            dbContext.AudioCommands.GetAllAsync().Returns(testResult);
            await audioCommands.AddAudioCommand(audioCommand);

            commandHandler.IsCoolDownExpiredWithMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            // Act
            await audioCommands.RunCommand(new() { Command = "testCommand" });

            // Assert
            await serviceBackbone.Received(1).SendChatMessage(Arg.Any<string>(), "yeah ummm... no... go away");
        }

        [Fact]
        public async Task RunCommand_ShouldSucceedForAll()
        {
            // Arrange
            var commandHandler = Substitute.For<ICommandHandler>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var sendAlerts = Substitute.For<ISendAlerts>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IUnitOfWork>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();

            scopeFactory.CreateScope().Returns(scope);

            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            var queryable = new List<AudioCommand> { }.AsQueryable().BuildMockDbSet();
            dbContext.AudioCommands.Find(x => true).ReturnsForAnyArgs(queryable);


            var audioCommands = new AudioCommands(sendAlerts, viewerFeature, scopeFactory,
                Substitute.For<ILogger<AudioCommands>>(), serviceBackbone,
                _langage, commandHandler);


            var audioCommand = new AudioCommand { CommandName = "testCommand", Disabled = false, MinimumRank = Rank.Viewer, GlobalCooldown = 5, UserCooldown = 5 };
            var testResult = new List<AudioCommand> { audioCommand };
            dbContext.AudioCommands.GetAllAsync().Returns(testResult);
            await audioCommands.AddAudioCommand(audioCommand);

            commandHandler.IsCoolDownExpiredWithMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            // Act
            await audioCommands.RunCommand(new() { Command = "testCommand" });

            // Assert
            sendAlerts.Received(1).QueueAlert(Arg.Any<AlertSound>());
        }
    }
}
