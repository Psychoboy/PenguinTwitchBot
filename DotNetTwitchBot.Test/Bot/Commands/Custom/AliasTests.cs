using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Custom;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Bot.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace DotNetTwitchBot.Tests.Bot.Commands.Custom
{
    public class AliasTests
    {
        private readonly ICommandHandler commandHandler;
        private readonly AliasModel testAlias;
        private readonly DbSet<AliasModel> aliasQueryable;
        private readonly DbSet<AliasModel> emptyAliasQueryable;
        private readonly Alias alias;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly IUnitOfWork dbContext;
        private readonly IServiceProvider serviceProvider;
        private readonly IServiceScope scope;
        private readonly IServiceBackbone serviceBackbone;

        public AliasTests()
        {
            scopeFactory = Substitute.For<IServiceScopeFactory>();
            dbContext = Substitute.For<IUnitOfWork>();
            serviceProvider = Substitute.For<IServiceProvider>();
            scope = Substitute.For<IServiceScope>();
            serviceBackbone = Substitute.For<IServiceBackbone>();
            commandHandler = Substitute.For<ICommandHandler>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            testAlias = new AliasModel { AliasName = "thealias", CommandName = "testcommand", Id = 1 };
            aliasQueryable = new List<AliasModel> { testAlias }.AsQueryable().BuildMockDbSet();
            emptyAliasQueryable = new List<AliasModel>().AsQueryable().BuildMockDbSet();

            alias = new Alias(scopeFactory, serviceBackbone, commandHandler);

        }

        [Fact]
        public async Task GetAliasesAsync_ShouldGetAllAliases()
        {
            //Arrange
            dbContext.Aliases.GetAllAsync().Returns(aliasQueryable);

            //Act
            var result = await alias.GetAliasesAsync();

            //Assert
            Assert.Contains(testAlias, result);
        }

        [Fact]
        public async Task GetAliasAsync_ShouldReturnAlias()
        {
            //Arrange
            dbContext.Aliases.Find(x => true).ReturnsForAnyArgs(aliasQueryable);

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
            await dbContext.Aliases.Received(1).AddAsync(newTestAlias);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task CreateOrUpdateAliasAsync_ShoulUpdate()
        {
            // Arrange
            var newTestAlias = new AliasModel { AliasName = "testAlias", CommandName = "testCommand", Id = 1 };

            // Act
            await alias.CreateOrUpdateAliasAsync(newTestAlias);

            // Assert
            dbContext.Aliases.Received(1).Update(newTestAlias);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task DeleteAliasAsync_ShouldRemove()
        {
            // Arrange

            // Act
            await alias.DeleteAliasAsync(testAlias);

            // Assert
            dbContext.Aliases.Received(1).Remove(testAlias);
            await dbContext.Received(1).SaveChangesAsync();

        }


        [Fact]
        public async Task RunCommand_RunAlias()
        {
            // Arrange
            dbContext.Aliases.Find(x => true).ReturnsForAnyArgs(aliasQueryable);
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
            dbContext.Aliases.Find(x => true).ReturnsForAnyArgs(aliasQueryable);
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
