using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Discord.Commands;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Bot.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using NSubstitute.Core.Arguments;
using Xunit;

namespace DotNetTwitchBot.Tests.Bot.Commands
{
    public class CommandHandlerTests
    {
        [Fact]
        public void GetCommand_Should_ReturnCommand_IfExists()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var commandService = Substitute.For<IBaseCommandService>();
           // commandService.ExecuteAsync().Returns(Task.CompletedTask);

            var commandHandler = new CommandHandler(logger, scopeFactory);
            commandHandler.AddCommand(new DefaultCommand { CustomCommandName = "testCommand" }, commandService);

            // Act
            var result = commandHandler.GetCommand("testCommand");

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void GetCommand_Should_ReturnNull_IfNotExists()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var commandHandler = new CommandHandler(logger, scopeFactory);

            // Act
            var result = commandHandler.GetCommand("nonExistentCommand");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void AddCommand_Should_AddCommand_WithDefaultProperties()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var commandService = Substitute.For<IBaseCommandService>();

            var commandHandler = new CommandHandler(logger, scopeFactory);

            // Act
            commandHandler.AddCommand(new DefaultCommand {CommandName = "testCommand", CustomCommandName = "testCommand" }, commandService);

            // Assert
            var result = commandHandler.GetCommand("testCommand");
            Assert.NotNull(result);
            Assert.Equal("testCommand", result.CommandProperties.CommandName);
        }

        [Fact]
        public void AddCommand_Should_AddCommand_WithNoneDefaultProperties()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var commandService = Substitute.For<IBaseCommandService>();

            var commandHandler = new CommandHandler(logger, scopeFactory);

            // Act
            commandHandler.AddCommand(new BaseCommandProperties { CommandName = "testCommand" }, commandService);

            // Assert
            var result = commandHandler.GetCommand("testCommand");
            Assert.NotNull(result);
            Assert.Equal("testCommand", result.CommandProperties.CommandName);
        }

        [Fact]
        public void UpdateCommandName_ShouldReplaceOldName()
        {
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var commandService = Substitute.For<IBaseCommandService>();

            var commandHandler = new CommandHandler(logger, scopeFactory);
            commandHandler.AddCommand(new DefaultCommand { CommandName = "testCommand", CustomCommandName = "testCommand" }, commandService);

            //Act
            commandHandler.UpdateCommandName("testCommand", "newCommand");

            Assert.NotNull(commandHandler.GetCommand("newCommand"));
            Assert.Null(commandHandler.GetCommand("testCommand"));

        }

        [Fact]
        public void RemoveCommand_ShouldRemoveCommand()
        {
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var commandService = Substitute.For<IBaseCommandService>();

            var commandHandler = new CommandHandler(logger, scopeFactory);
            commandHandler.AddCommand(new DefaultCommand { CommandName = "testCommand", CustomCommandName = "testCommand" }, commandService);

            //Act
            commandHandler.RemoveCommand("testCommand");

            Assert.Null(commandHandler.GetCommand("testCommand"));
        }



        [Fact]
        public async Task UpdateDefaultCommand_Should_UpdateDefaultCommand()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var dbContext = Substitute.For<IDefaultCommandRepository>();

            var commandHandler = new CommandHandler(logger, scopeFactory);
            var commandService = Substitute.For<IBaseCommandService>();


            var defaultCommand = new DefaultCommand { Id = 1, CustomCommandName = "newCommand", CommandName = "testCommand" };
            var testCommand = new DefaultCommand { Id = 1, CommandName = "testCommand", CustomCommandName = "testCommand" };
            commandHandler.AddCommand(testCommand, commandService);
            var queryable = new List<DefaultCommand> { testCommand }.AsQueryable().BuildMockDbSet();
            dbContext.Find(x => true).ReturnsForAnyArgs(queryable);
            

            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(typeof(IDefaultCommandRepository)).Returns(dbContext);

            var scope = Substitute.For<IServiceScope>();
            scope.ServiceProvider.Returns(serviceProvider);

            scopeFactory.CreateScope().Returns(scope);

           

            // Act
            await commandHandler.UpdateDefaultCommand(defaultCommand);

            // Assert
            dbContext.Received(1).Update(defaultCommand);
        }


        [Fact]
        public async Task UpdateDefaultCommand_ShouldNot_UpdateDefaultCommand_NoId()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var dbContext = Substitute.For<IDefaultCommandRepository>();

            var commandHandler = new CommandHandler(logger, scopeFactory);
            var commandService = Substitute.For<IBaseCommandService>();


            var defaultCommand = new DefaultCommand { CustomCommandName = "newCommand", CommandName = "testCommand" };
            var testCommand = new DefaultCommand { Id = 1, CommandName = "testCommand", CustomCommandName = "testCommand" };
            commandHandler.AddCommand(testCommand, commandService);
            var queryable = new List<DefaultCommand> { testCommand }.AsQueryable().BuildMockDbSet();
            dbContext.Find(x => true).ReturnsForAnyArgs(queryable);


            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(typeof(IDefaultCommandRepository)).Returns(dbContext);

            var scope = Substitute.For<IServiceScope>();
            scope.ServiceProvider.Returns(serviceProvider);

            scopeFactory.CreateScope().Returns(scope);



            // Act
            await commandHandler.UpdateDefaultCommand(defaultCommand);

            // Assert
            dbContext.Received(0).Update(defaultCommand);
        }


        [Fact]
        public async Task UpdateDefaultCommand_ShouldNot_UpdateDefaultCommand_NotAdded()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var dbContext = Substitute.For<IDefaultCommandRepository>();

            var commandHandler = new CommandHandler(logger, scopeFactory);
            var commandService = Substitute.For<IBaseCommandService>();


            var defaultCommand = new DefaultCommand { Id = 1, CustomCommandName = "newCommand", CommandName = "testCommand" };
           
            var queryable = new List<DefaultCommand> {  }.AsQueryable().BuildMockDbSet();
            dbContext.Find(x => true).ReturnsForAnyArgs(queryable);


            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(typeof(IDefaultCommandRepository)).Returns(dbContext);

            var scope = Substitute.For<IServiceScope>();
            scope.ServiceProvider.Returns(serviceProvider);

            scopeFactory.CreateScope().Returns(scope);

            // Act
            await commandHandler.UpdateDefaultCommand(defaultCommand);

            // Assert
            dbContext.Received(0).Update(defaultCommand);
        }

        [Fact]
        public async Task GetDefaultCommandFromDb_ShouldReturnCorrectCommand()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var dbContext = Substitute.For<IDefaultCommandRepository>();

            var commandHandler = new CommandHandler(logger, scopeFactory);
            var commandService = Substitute.For<IBaseCommandService>();


            var defaultCommand = new DefaultCommand { Id = 1, CustomCommandName = "testCommand", CommandName = "testCommand" };

            var queryable = new List<DefaultCommand> { defaultCommand }.AsQueryable().BuildMockDbSet();
            dbContext.Find(x => true).ReturnsForAnyArgs(queryable);


            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(typeof(IDefaultCommandRepository)).Returns(dbContext);

            var scope = Substitute.For<IServiceScope>();
            scope.ServiceProvider.Returns(serviceProvider);

            scopeFactory.CreateScope().Returns(scope);

            //defaultCommandRepository.Find(x => true).ReceivedWithAnyArgs(var queryable = new List<DefaultCommand> { testCommand }.AsQueryable().BuildMockDbSet();)
            //    .FirstOrDefaultAsync().Returns(expectedCommand);

            // Act
            var result = await commandHandler.GetDefaultCommandFromDb("testCommand");

            // Assert
            Assert.Equal(defaultCommand, result);
        }

        [Fact]
        public async Task GetDefaultCommandById_ShouldReturnCorrectCommand()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var dbContext = Substitute.For<IDefaultCommandRepository>();

            var commandHandler = new CommandHandler(logger, scopeFactory);
            var commandService = Substitute.For<IBaseCommandService>();


            var defaultCommand = new DefaultCommand { Id = 1, CustomCommandName = "testCommand", CommandName = "testCommand" };

            var queryable = new List<DefaultCommand> { defaultCommand }.AsQueryable().BuildMockDbSet();
            dbContext.Find(x => true).ReturnsForAnyArgs(queryable);


            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(typeof(IDefaultCommandRepository)).Returns(dbContext);

            var scope = Substitute.For<IServiceScope>();
            scope.ServiceProvider.Returns(serviceProvider);

            scopeFactory.CreateScope().Returns(scope);

            //defaultCommandRepository.Find(Arg.Any<Func<DefaultCommand, bool>>())
            //    .FirstOrDefaultAsync().Returns(expectedCommand);

            // Act
            var result = await commandHandler.GetDefaultCommandById(1);

            // Assert
            Assert.Equal(defaultCommand, result);
        }

        [Fact]
        public async Task GetDefaultCommandsFromDb_ShouldReturnListOfCommands()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var dbContext = Substitute.For<IDefaultCommandRepository>();

            var commandHandler = new CommandHandler(logger, scopeFactory);
            var commandService = Substitute.For<IBaseCommandService>();


            var defaultCommand = new DefaultCommand { Id = 1, CustomCommandName = "testCommand", CommandName = "testCommand" };
            var commands = new List<DefaultCommand>
            {
                new DefaultCommand { Id = 1, CommandName = "cmd1" },
                new DefaultCommand { Id = 2, CommandName = "cmd2" }
            };
            var queryable = commands.AsQueryable().BuildMockDbSet();

            dbContext.Find(x => true).ReturnsForAnyArgs(queryable);


            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(typeof(IDefaultCommandRepository)).Returns(dbContext);

            var scope = Substitute.For<IServiceScope>();
            scope.ServiceProvider.Returns(serviceProvider);

            scopeFactory.CreateScope().Returns(scope);
            var expectedCommands =
            dbContext.GetAllAsync().Returns(commands);

            // Act
            var result = await commandHandler.GetDefaultCommandsFromDb();

            // Assert
            Assert.Equal(commands, result);
        }

        [Fact]
        public void IsCoolDownExpired_ShouldReturnTrue_WhenNoCooldowns()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var commandHandler = new CommandHandler(logger, scopeFactory);
            var user = "testUser";
            var command = "testCommand";

            // Act
            var result = commandHandler.IsCoolDownExpired(user, command);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsCoolDownExpired_ShouldReturnFalse_WhenGlobalCooldownActive()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var commandHandler = new CommandHandler(logger, scopeFactory);
            var user = "testUser";
            var command = "testCommand";
            var globalCooldown = DateTime.Now.AddSeconds(10);
            commandHandler.AddGlobalCooldown(command, globalCooldown);

            // Act
            var result = commandHandler.IsCoolDownExpired(user, command);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsCoolDownExpiredWithMessage_ShouldReturnFalse_WhenCooldownActive()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var dbContext = Substitute.For<IDefaultCommandRepository>();

            var commandHandler = new CommandHandler(logger, scopeFactory);
            var commandService = Substitute.For<IBaseCommandService>();


            var defaultCommand = new DefaultCommand { Id = 1, CustomCommandName = "testCommand", CommandName = "testCommand" };

            var queryable = new List<DefaultCommand> { defaultCommand }.AsQueryable().BuildMockDbSet();
            dbContext.Find(x => true).ReturnsForAnyArgs(queryable);


            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(typeof(IDefaultCommandRepository)).Returns(dbContext);

            var scope = Substitute.For<IServiceScope>();
            scope.ServiceProvider.Returns(serviceProvider);

            scopeFactory.CreateScope().Returns(scope);
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            serviceProvider.GetService(typeof(IServiceBackbone)).Returns(serviceBackbone);

            var displayName = "TestUser";

            var user = "testUser";
            var command = "testCommand";
            var cooldown = DateTime.Now.AddSeconds(10);
            commandHandler.AddCoolDown(user, command, cooldown);

            // Act
            var result = await commandHandler.IsCoolDownExpiredWithMessage(user, displayName, command);

            // Assert
            Assert.False(result);
            await serviceBackbone.Received(1).SendChatMessage(displayName, Arg.Any<string>());
        }

        private IQueryable<DefaultCommand> GetDefaultCommandDbSet()
        {
            var data = new List<DefaultCommand>
            {
                new DefaultCommand { Id = 1, CommandName = "testCommand1" },
                new DefaultCommand { Id = 2, CommandName = "testCommand2" }
            }.AsQueryable();

            return data;
        }
    }
}
