using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotNetTwitchBot.Tests.Bot.Commands
{
    public class CommandHandlerTests
    {
        [Fact]
        public void GetCommand_CommandExists_ReturnsCommand()
        {
            // Arrange
            var loggerSubstitute = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactorySubstitute = Substitute.For<IServiceScopeFactory>();
            var commandHandler = new CommandHandler(loggerSubstitute, scopeFactorySubstitute);

            var commandServiceSubstitute = Substitute.For<IBaseCommandService>();
            var commandProperties = new DefaultCommand { CustomCommandName = "testCommand" };
            commandHandler.AddCommand(commandProperties, commandServiceSubstitute);

            // Act
            var result = commandHandler.GetCommand("testCommand");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(commandServiceSubstitute, result!.CommandService);
            Assert.Equal(commandProperties, result!.CommandProperties);
        }

        [Fact]
        public void GetCommand_CommandDoesNotExist_ReturnsNull()
        {
            // Arrange
            var loggerSubstitute = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactorySubstitute = Substitute.For<IServiceScopeFactory>();
            var commandHandler = new CommandHandler(loggerSubstitute, scopeFactorySubstitute);

            // Act
            var result = commandHandler.GetCommand("nonExistentCommand");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void AddCommand_AddsCommandToCommandsDictionary()
        {
            // Arrange
            var loggerSubstitute = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactorySubstitute = Substitute.For<IServiceScopeFactory>();
            var commandHandler = new CommandHandler(loggerSubstitute, scopeFactorySubstitute);

            var commandServiceSubstitute = Substitute.For<IBaseCommandService>();
            var commandProperties = new DefaultCommand { CustomCommandName = "testCommand" };

            // Act
            commandHandler.AddCommand(commandProperties, commandServiceSubstitute);

            // Assert
            Assert.NotNull(commandHandler.GetCommand("testCommand"));
            Assert.Equal(commandServiceSubstitute, commandHandler.GetCommand("testCommand")?.CommandService);
            Assert.Equal(commandProperties, commandHandler.GetCommand("testCommand")?.CommandProperties);
        }

        [Fact]
        public void UpdateCommandName_UpdatesCommandNameInCommandsDictionary()
        {
            // Arrange
            var loggerSubstitute = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactorySubstitute = Substitute.For<IServiceScopeFactory>();
            var commandHandler = new CommandHandler(loggerSubstitute, scopeFactorySubstitute);

            var oldCommandName = "oldCommand";
            var newCommandName = "newCommand";
            var commandServiceSubstitute = Substitute.For<IBaseCommandService>();
            var commandProperties = new DefaultCommand { CustomCommandName = oldCommandName };
            commandHandler.AddCommand(commandProperties, commandServiceSubstitute);

            // Act
            commandHandler.UpdateCommandName(oldCommandName, newCommandName);

            // Assert
            Assert.Null(commandHandler.GetCommand(oldCommandName));
            Assert.NotNull(commandHandler.GetCommand(newCommandName));
            Assert.Equal(commandServiceSubstitute, commandHandler.GetCommand(newCommandName)?.CommandService);
            Assert.Equal(commandProperties, commandHandler.GetCommand(newCommandName)?.CommandProperties);
        }

        [Fact]
        public void RemoveCommand_RemovesCommandFromCommandsDictionary()
        {
            // Arrange
            var loggerSubstitute = Substitute.For<ILogger<CommandHandler>>();
            var scopeFactorySubstitute = Substitute.For<IServiceScopeFactory>();
            var commandHandler = new CommandHandler(loggerSubstitute, scopeFactorySubstitute);

            var commandName = "testCommand";
            var commandServiceSubstitute = Substitute.For<IBaseCommandService>();
            var commandProperties = new DefaultCommand { CustomCommandName = commandName };
            commandHandler.AddCommand(commandProperties, commandServiceSubstitute);

            // Act
            commandHandler.RemoveCommand(commandName);

            // Assert
            Assert.Null(commandHandler.GetCommand(commandName));
        }
    }
}
