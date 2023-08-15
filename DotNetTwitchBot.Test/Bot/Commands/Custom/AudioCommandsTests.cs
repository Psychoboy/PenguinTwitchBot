using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using DotNetTwitchBot.Bot.Commands.Custom;
using DotNetTwitchBot.Bot.Repository;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using MockQueryable.NSubstitute;
using NSubstitute.Extensions;
using System.Linq.Expressions;

namespace DotNetTwitchBot.Tests
{
    public class AudioCommandsTests
    {
        [Fact]
        public async Task AddAudioCommand_ShouldAddCommandToDatabase()
        {
            // Arrange
            var sendAlerts = Substitute.For<ISendAlerts>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IAudioCommandsRepository>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();

            scopeFactory.CreateScope().Returns(scope);

            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IAudioCommandsRepository)).Returns(dbContext);

            var queryable = new List<AudioCommand> { }.AsQueryable().BuildMockDbSet();
            dbContext.Find(x => true).ReturnsForAnyArgs(queryable);

            var audioCommands = new AudioCommands(sendAlerts, viewerFeature, scopeFactory,
                Substitute.For<ILogger<AudioCommands>>(), Substitute.For<IServiceBackbone>(),
                Substitute.For<ICommandHandler>());


            var audioCommand = new AudioCommand { CommandName = "testCommand"};
            // Act
            await audioCommands.AddAudioCommand(audioCommand);

            // Assert
            await dbContext.Received(1).AddAsync(audioCommand);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task AddAudioCommand_ShouldNotAddCommandToDatabase()
        {
            // Arrange
            var sendAlerts = Substitute.For<ISendAlerts>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IAudioCommandsRepository>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();

            scopeFactory.CreateScope().Returns(scope);

            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IAudioCommandsRepository)).Returns(dbContext);
            var audioCommand = new AudioCommand { CommandName = "testCommand" };
            var queryable = new List<AudioCommand> { audioCommand }.AsQueryable().BuildMockDbSet();
            dbContext.Find(x => true).ReturnsForAnyArgs(queryable);

            var audioCommands = new AudioCommands(sendAlerts, viewerFeature, scopeFactory,
                Substitute.For<ILogger<AudioCommands>>(), Substitute.For<IServiceBackbone>(),
                Substitute.For<ICommandHandler>());

            // Act
            await audioCommands.AddAudioCommand(audioCommand);

            // Assert
            await dbContext.Received(0).AddAsync(audioCommand);
            await dbContext.Received(0).SaveChangesAsync();
        }

        [Fact]
        public async Task SaveAudioCommand_ShouldSave()
        {
            // Arrange
            var sendAlerts = Substitute.For<ISendAlerts>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IAudioCommandsRepository>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();

            scopeFactory.CreateScope().Returns(scope);

            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IAudioCommandsRepository)).Returns(dbContext);

            var queryable = new List<AudioCommand> { }.AsQueryable().BuildMockDbSet();
            dbContext.Find(x => true).ReturnsForAnyArgs(queryable);

            var audioCommands = new AudioCommands(sendAlerts, viewerFeature, scopeFactory,
                Substitute.For<ILogger<AudioCommands>>(), Substitute.For<IServiceBackbone>(),
                Substitute.For<ICommandHandler>());


            var audioCommand = new AudioCommand { CommandName = "testCommand" };
            // Act
            await audioCommands.SaveAudioCommand(audioCommand);

            // Assert
            dbContext.Received(1).Update(audioCommand);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GetAudioCommand_ShouldGet()
        {
            // Arrange
            var sendAlerts = Substitute.For<ISendAlerts>();
            var viewerFeature = Substitute.For<IViewerFeature>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var dbContext = Substitute.For<IAudioCommandsRepository>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();

            scopeFactory.CreateScope().Returns(scope);

            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IAudioCommandsRepository)).Returns(dbContext);

            var audiCommand = new AudioCommand { Id = 1, CommandName = "TestCommand" };
            var queryable = new List<AudioCommand> { audiCommand }.AsQueryable().BuildMockDbSet();
            dbContext.Find(x => true).ReturnsForAnyArgs(queryable);

            var audioCommands = new AudioCommands(sendAlerts, viewerFeature, scopeFactory,
                Substitute.For<ILogger<AudioCommands>>(), Substitute.For<IServiceBackbone>(),
                Substitute.For<ICommandHandler>());


            // Act
            var testAudioCommand = await audioCommands.GetAudioCommand(1);

            // Assert
            Assert.Equal(audiCommand, testAudioCommand);
        }
    }
}
