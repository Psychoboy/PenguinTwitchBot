using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Custom;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using NSubstitute;
using Xunit;

namespace DotNetTwitchBot.Tests.Bot.Commands.Custom
{
    public class AliasTests
    {
        [Fact]
        public async Task GetAliasesAsync_ReturnsListOfAliases()
        {
            // Arrange
            var aliasDbSubstitute = Substitute.For<DotNetTwitchBot.Bot.DataAccess.IAlias>();
            var serviceBackboneSubstitute = Substitute.For<IServiceBackbone>();
            var commandHandlerSubstitute = Substitute.For<ICommandHandler>();

            var aliases = new List<AliasModel>
            {
                new AliasModel { AliasName = "alias1", CommandName = "command1" },
                new AliasModel { AliasName = "alias2", CommandName = "command2" }
            };
            aliasDbSubstitute.GetAliasesAsync().Returns(aliases);

            var alias = new Alias(aliasDbSubstitute, serviceBackboneSubstitute, commandHandlerSubstitute);

            // Act
            var result = await alias.GetAliasesAsync();

            // Assert
            Assert.Equal(aliases, result);
        }

        [Fact]
        public async Task GetAliasAsync_ValidId_ReturnsAlias()
        {
            // Arrange
            var aliasDbSubstitute = Substitute.For<DotNetTwitchBot.Bot.DataAccess.IAlias>();
            var serviceBackboneSubstitute = Substitute.For<IServiceBackbone>();
            var commandHandlerSubstitute = Substitute.For<ICommandHandler>();

            var aliasModel = new AliasModel { Id = 1, AliasName = "alias1", CommandName = "command1" };
            aliasDbSubstitute.GetAliasAsync(1).Returns(aliasModel);

            var alias = new Alias(aliasDbSubstitute, serviceBackboneSubstitute, commandHandlerSubstitute);

            // Act
            var result = await alias.GetAliasAsync(1);

            // Assert
            Assert.Equal(aliasModel, result);
        }

        [Fact]
        public async Task GetAliasAsync_InvalidId_ReturnsNull()
        {
            // Arrange
            var aliasDbSubstitute = Substitute.For<DotNetTwitchBot.Bot.DataAccess.IAlias>();
            var serviceBackboneSubstitute = Substitute.For<IServiceBackbone>();
            var commandHandlerSubstitute = Substitute.For<ICommandHandler>();

            aliasDbSubstitute.GetAliasAsync(Arg.Any<int>()).Returns(Task.FromResult<AliasModel?>(null));

            var alias = new Alias(aliasDbSubstitute, serviceBackboneSubstitute, commandHandlerSubstitute);

            // Act
            var result = await alias.GetAliasAsync(1);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task CreateOrUpdateAliasAsync_NewAlias_AddsAliasToDb()
        {
            // Arrange
            var aliasDbSubstitute = Substitute.For<DotNetTwitchBot.Bot.DataAccess.IAlias>();
            var serviceBackboneSubstitute = Substitute.For<IServiceBackbone>();
            var commandHandlerSubstitute = Substitute.For<ICommandHandler>();

            var alias = new Alias(aliasDbSubstitute, serviceBackboneSubstitute, commandHandlerSubstitute);
            var newAlias = new AliasModel { AliasName = "newAlias", CommandName = "newCommand" };

            // Act
            await alias.CreateOrUpdateAliasAsync(newAlias);

            // Assert
            await aliasDbSubstitute.Received(1).CreateOrUpdateAliasAsync(newAlias);
        }

        [Fact]
        public async Task DeleteAliasAsync_DeletesAliasFromDb()
        {
            // Arrange
            var aliasDbSubstitute = Substitute.For<DotNetTwitchBot.Bot.DataAccess.IAlias>();
            var serviceBackboneSubstitute = Substitute.For<IServiceBackbone>();
            var commandHandlerSubstitute = Substitute.For<ICommandHandler>();

            var alias = new Alias(aliasDbSubstitute, serviceBackboneSubstitute, commandHandlerSubstitute);
            var aliasToDelete = new AliasModel { Id = 1, AliasName = "aliasToDelete", CommandName = "command" };

            // Act
            await alias.DeleteAliasAsync(aliasToDelete);

            // Assert
            await aliasDbSubstitute.Received(1).DeleteAliasAsync(aliasToDelete);
        }

        [Fact]
        public async Task RunCommand_AliasFound_RunsCommand()
        {
            // Arrange
            var aliasDbSubstitute = Substitute.For<DotNetTwitchBot.Bot.DataAccess.IAlias>();
            var serviceBackboneSubstitute = Substitute.For<IServiceBackbone>();
            var commandHandlerSubstitute = Substitute.For<ICommandHandler>();

            var aliasModel = new AliasModel { AliasName = "alias", CommandName = "command" };
            aliasDbSubstitute.GetAliasAsync(Arg.Any<string>()).Returns(aliasModel);

            var alias = new Alias(aliasDbSubstitute, serviceBackboneSubstitute, commandHandlerSubstitute);
            var commandEventArgs = new CommandEventArgs { FromAlias = false, Command = "alias" };

            // Act
            var result = await alias.RunCommand(commandEventArgs);

            // Assert
            Assert.True(result);
            Assert.True(commandEventArgs.FromAlias);
            await serviceBackboneSubstitute.Received(1).RunCommand(commandEventArgs);
        }

        [Fact]
        public async Task RunCommand_InvalidAlias_DoesNotRunCommand()
        {
            // Arrange
            var aliasDbSubstitute = Substitute.For<DotNetTwitchBot.Bot.DataAccess.IAlias>();
            var serviceBackboneSubstitute = Substitute.For<IServiceBackbone>();
            var commandHandlerSubstitute = Substitute.For<ICommandHandler>();

            aliasDbSubstitute.GetAliasAsync(Arg.Any<string>()).Returns(Task.FromResult<AliasModel?>(null));

            var alias = new Alias(aliasDbSubstitute, serviceBackboneSubstitute, commandHandlerSubstitute);
            var commandEventArgs = new CommandEventArgs { FromAlias = false, Command = "nonExistentAlias" };

            // Act
            var result = await alias.RunCommand(commandEventArgs);

            // Assert
            Assert.False(result);
            Assert.False(commandEventArgs.FromAlias);
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            await serviceBackboneSubstitute.DidNotReceiveWithAnyArgs().RunCommand(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        // Add more tests for other methods
    }
}
