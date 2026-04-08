using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Alias;
using DotNetTwitchBot.Bot.Commands.Alias.Requests;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Tests.Bot.Commands.Custom
{
    public class AliasTests
    {
        private readonly ICommandHandler commandHandler;
        private readonly AliasModel testAlias;
        private readonly List<AliasModel> aliasQueryable;
        private readonly List<AliasModel> emptyAliasQueryable;
        private readonly Alias alias;
        private readonly DotNetTwitchBot.Application.Notifications.IPenguinDispatcher dispatcher;
        private readonly IServiceBackbone serviceBackbone;

        public AliasTests()
        {
            serviceBackbone = Substitute.For<IServiceBackbone>();
            commandHandler = Substitute.For<ICommandHandler>();
            dispatcher = Substitute.For<DotNetTwitchBot.Application.Notifications.IPenguinDispatcher>();

            testAlias = new AliasModel { AliasName = "thealias", CommandName = "testcommand", Id = 1 };
            aliasQueryable = new List<AliasModel> { testAlias };
            emptyAliasQueryable = new List<AliasModel>();

            alias = new Alias(dispatcher, serviceBackbone, Substitute.For<ILogger<Alias>>(), commandHandler);

        }

        [Fact]
        public async Task GetAliasesAsync_ShouldGetAllAliases()
        {
            //Arrange
            dispatcher.Send(Arg.Any<GetAliases>()).ReturnsForAnyArgs(aliasQueryable);

            //Act
            var result = await alias.GetAliasesAsync();

            //Assert
            Assert.Contains(testAlias, result);
        }

        [Fact]
        public async Task GetAliasAsync_ShouldReturnAlias()
        {
            //Arrange
            dispatcher.Send(Arg.Any<GetAliasById>()).ReturnsForAnyArgs(testAlias);

            //Act
            var result = await alias.GetAliasAsync(1);

            //Assert
            Assert.Equal(testAlias, result);
        }


        [Fact]
        public async Task CreateOrUpdateAliasAsync_ShouldCreate()
        {
            // Arrange
            var newTestAlias = new AliasModel { AliasName = "testAlias", CommandName = "testCommand" };

            // Act
            await alias.CreateOrUpdateAliasAsync(newTestAlias);

            // Assert
            await dispatcher.Received(1).Send(Arg.Any<CreateAlias>());
        }

        [Fact]
        public async Task CreateOrUpdateAliasAsync_ShoulUpdate()
        {
            // Arrange
            var newTestAlias = new AliasModel { AliasName = "testAlias", CommandName = "testCommand", Id = 1 };

            // Act
            await alias.CreateOrUpdateAliasAsync(newTestAlias);

            // Assert
            await dispatcher.Received(1).Send(Arg.Any<UpdateAlias>());
        }

        [Fact]
        public async Task DeleteAliasAsync_ShouldRemove()
        {
            // Arrange

            // Act
            await alias.DeleteAliasAsync(testAlias);

            // Assert
            await dispatcher.Received(1).Send(Arg.Any<DeleteAlias>());

        }


        [Fact]
        public async Task RunCommand_RunAlias()
        {
            // Arrange
            //dbContext.Aliases.Find(x => true).ReturnsForAnyArgs(aliasQueryable);
            dispatcher.Send(Arg.Any<GetAliasByName>()).Returns(testAlias);
            var testCommand = new DotNetTwitchBot.Bot.Events.Chat.CommandEventArgs();
            // Act
            var result = await alias.RunCommand(testCommand);

            // Assert
            Assert.True(result);
            await serviceBackbone.Received(1).RunCommand(testCommand);
        }

        [Fact]
        public async Task RunCommand_RunAlias_ShouldFail()
        {
            // Arrange
            dispatcher.Send(Arg.Any<GetAliasByName>()).Returns(testAlias);
            var testCommand = new DotNetTwitchBot.Bot.Events.Chat.CommandEventArgs();
            testCommand.FromAlias = true;
            // Act
            var result = await alias.RunCommand(testCommand);

            // Assert
            Assert.False(result);
            await serviceBackbone.Received(0).RunCommand(testCommand);
        }
    }
}
