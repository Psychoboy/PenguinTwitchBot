using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Moderation;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Bot.Models.Commands;
using DotNetTwitchBot.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable;
using MockQueryable.NSubstitute;
using Moq;
using NSubstitute;
using Xunit;

namespace DotNetTwitchBot.Tests.Bot.Commands
{
    public class CommandHandlerTests
    {
        private readonly Mock<ILogger<CommandHandler>> _loggerMock;
        private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
        private readonly Mock<IKnownBots> _knownBotsMock;
        private readonly CommandHandler _commandHandler;

        public CommandHandlerTests()
        {
            _loggerMock = new Mock<ILogger<CommandHandler>>();
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();
            _knownBotsMock = new Mock<IKnownBots>();
            _commandHandler = new CommandHandler(_loggerMock.Object, _scopeFactoryMock.Object, _knownBotsMock.Object);
        }

        [Fact]
        public void AddCommand_ShouldAddCommand()
        {
            var commandProperties = new BaseCommandProperties
            {
                CommandName = "testCommand"
            };
            var commandServiceMock = new Mock<IBaseCommandService>();

            _commandHandler.AddCommand(commandProperties, commandServiceMock.Object);

            var command = _commandHandler.GetCommand("testCommand");
            Assert.NotNull(command);
            Assert.Equal(commandProperties, command.CommandProperties);
        }

        [Fact]
        public void RemoveCommand_ShouldRemoveCommand()
        {
            var commandProperties = new BaseCommandProperties
            {
                CommandName = "testCommand"
            };
            var commandServiceMock = new Mock<IBaseCommandService>();

            _commandHandler.AddCommand(commandProperties, commandServiceMock.Object);
            _commandHandler.RemoveCommand("testCommand");

            var command = _commandHandler.GetCommand("testCommand");
            Assert.Null(command);
        }

        [Fact]
        public void UpdateCommandName_ShouldUpdateCommandName()
        {
            var commandProperties = new BaseCommandProperties
            {
                CommandName = "oldCommandName"
            };
            var commandServiceMock = new Mock<IBaseCommandService>();

            _commandHandler.AddCommand(commandProperties, commandServiceMock.Object);
            _commandHandler.UpdateCommandName("oldCommandName", "newCommandName");

            var oldCommand = _commandHandler.GetCommand("oldCommandName");
            var newCommand = _commandHandler.GetCommand("newCommandName");

            Assert.Null(oldCommand);
            Assert.NotNull(newCommand);
            Assert.Equal(commandProperties, newCommand.CommandProperties);
        }

        [Fact]
        public async Task IsCoolDownExpired_ShouldReturnTrue_WhenNoCooldown()
        {
            var scopeMock = new Mock<IServiceScope>();
            var unitOfWorkMock = new Mock<IUnitOfWork>();
            scopeMock.Setup(s => s.ServiceProvider.GetService(typeof(IUnitOfWork))).Returns(unitOfWorkMock.Object);
            _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var testCooldown = new List<CurrentCooldowns> { };
            var queryable = testCooldown.BuildMockDbSet().AsQueryable(); ;

            unitOfWorkMock.Setup(x => x.Cooldowns.Find(y => y.CommandName.Equals("testCommand") && y.IsGlobal == true)).Returns(queryable);
            unitOfWorkMock.Setup(x => x.Cooldowns.Find(y => y.CommandName.Equals("testCommand") && y.IsGlobal == false)).Returns(queryable);
            unitOfWorkMock.Setup(x => x.Cooldowns.Find(y => y.CommandName.Equals("testCommand") && y.UserName.Equals("testUser"))).Returns(queryable);


            var result = await _commandHandler.IsCoolDownExpired("testUser", "testCommand");

            Assert.True(result);
        }

        [Fact]
        public async Task IsCoolDownExpired_ShouldReturnFalse_WhenGlobalCooldownNotExpired()
        {
            var commandProperties = new BaseCommandProperties
            {
                CommandName = "testCommand"
            };
            var commandServiceMock = new Mock<IBaseCommandService>();
            var scopeMock = new Mock<IServiceScope>();
            var unitOfWorkMock = new Mock<IUnitOfWork>();
            scopeMock.Setup(s => s.ServiceProvider.GetService(typeof(IUnitOfWork))).Returns(unitOfWorkMock.Object);
            _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var testCooldown = new List<CurrentCooldowns> { new CurrentCooldowns { NextGlobalCooldownTime = DateTime.MaxValue, NextUserCooldownTime = DateTime.MaxValue } };
            var queryable = testCooldown.BuildMockDbSet().AsQueryable(); ;

            unitOfWorkMock.Setup(x => x.Cooldowns.Find(y => y.CommandName.Equals("testCommand") && y.IsGlobal == true)).Returns(queryable);
            _commandHandler.AddCommand(commandProperties, commandServiceMock.Object);
            await _commandHandler.AddGlobalCooldown("testCommand", DateTime.Now.AddMinutes(1));

            var result = await _commandHandler.IsCoolDownExpired("testUser", "testCommand");

            Assert.False(result);
        }

        [Fact]
        public async Task IsCoolDownExpired_ShouldReturnFalse_WhenUserCooldownNotExpired()
        {
            var commandProperties = new BaseCommandProperties
            {
                CommandName = "testCommand"
            };
            var commandServiceMock = new Mock<IBaseCommandService>();
            var scopeMock = new Mock<IServiceScope>();
            var unitOfWorkMock = new Mock<IUnitOfWork>();
            scopeMock.Setup(s => s.ServiceProvider.GetService(typeof(IUnitOfWork))).Returns(unitOfWorkMock.Object);
            _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var testCooldown = new List<CurrentCooldowns> { new CurrentCooldowns { NextGlobalCooldownTime = DateTime.MaxValue, NextUserCooldownTime = DateTime.MaxValue } };
            var queryable = testCooldown.BuildMockDbSet().AsQueryable(); ;

            unitOfWorkMock.Setup(x => x.Cooldowns.Find(y => y.CommandName.Equals("testCommand") && y.IsGlobal == true)).Returns(queryable);
            unitOfWorkMock.Setup(x => x.Cooldowns.Find(y => y.CommandName.Equals("testCommand") && y.IsGlobal == false)).Returns(queryable);
            unitOfWorkMock.Setup(x => x.Cooldowns.Find(y => y.CommandName.Equals("testCommand") && y.UserName.Equals("testUser"))).Returns(queryable);

            _commandHandler.AddCommand(commandProperties, commandServiceMock.Object);
            await _commandHandler.AddCoolDown("testUser", "testCommand", DateTime.Now.AddMinutes(1));

            var result = await _commandHandler.IsCoolDownExpired("testUser", "testCommand");

            Assert.False(result);
        }

        [Fact]
        public async Task IsCoolDownExpired_ShouldReturnTrue_WhenCooldownExpired()
        {
            var commandProperties = new BaseCommandProperties
            {
                CommandName = "testCommand"
            };
            var commandServiceMock = new Mock<IBaseCommandService>();
            var scopeMock = new Mock<IServiceScope>();
            var unitOfWorkMock = new Mock<IUnitOfWork>();
            scopeMock.Setup(s => s.ServiceProvider.GetService(typeof(IUnitOfWork))).Returns(unitOfWorkMock.Object);
            _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var testCooldown = new List<CurrentCooldowns> { new CurrentCooldowns { NextGlobalCooldownTime = DateTime.MinValue, NextUserCooldownTime = DateTime.MinValue } };
            var queryable = testCooldown.BuildMockDbSet().AsQueryable();

            unitOfWorkMock.Setup(x => x.Cooldowns.Find(y => y.CommandName.Equals("testCommand") && y.IsGlobal == true)).Returns(queryable);
            unitOfWorkMock.Setup(x => x.Cooldowns.Find(y => y.CommandName.Equals("testCommand") && y.IsGlobal == false)).Returns(queryable);
            unitOfWorkMock.Setup(x => x.Cooldowns.Find(y => y.CommandName.Equals("testCommand") && y.UserName.Equals("testUser"))).Returns(queryable);

            _commandHandler.AddCommand(commandProperties, commandServiceMock.Object);
            await _commandHandler.AddCoolDown("testUser", "testCommand", DateTime.Now.AddSeconds(-1));

            var result = await _commandHandler.IsCoolDownExpired("testUser", "testCommand");

            Assert.True(result);
        }

        [Fact]
        public async Task CheckPermission_ShouldPass_ForViewer()
        {
            var commandProperties = new BaseCommandProperties
            {
                MinimumRank = Rank.Viewer
            };
            var eventArgs = new CommandEventArgs
            {
                Name = "testuser"
            };

            var result = await _commandHandler.CheckPermission(commandProperties, eventArgs);

            Assert.True(result);
        }

        [Fact]
        public async Task CheckPermission_ShouldFail_ForNonFollower()
        {
            var commandProperties = new BaseCommandProperties
            {
                MinimumRank = Rank.Follower
            };
            var eventArgs = new CommandEventArgs
            {
                Name = "testuser"
            };

            var viewerFeatureMock = new Mock<DotNetTwitchBot.Bot.Commands.Features.IViewerFeature>();
            viewerFeatureMock.Setup(v => v.IsFollowerByUsername(It.IsAny<string>())).ReturnsAsync(false);

            var scopeMock = new Mock<IServiceScope>();
            scopeMock.Setup(s => s.ServiceProvider.GetService(typeof(DotNetTwitchBot.Bot.Commands.Features.IViewerFeature))).Returns(viewerFeatureMock.Object);
            _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var result = await _commandHandler.CheckPermission(commandProperties, eventArgs);

            Assert.False(result);
        }

        [Fact]
        public async Task CheckPermission_ShouldPass_ForSubscriber()
        {
            var commandProperties = new BaseCommandProperties
            {
                MinimumRank = Rank.Subscriber
            };
            var eventArgs = new CommandEventArgs
            {
                Name = "testuser",
                IsSub = true
            };

            var result = await _commandHandler.CheckPermission(commandProperties, eventArgs);

            Assert.True(result);
        }

        [Fact]
        public async Task CheckPermission_ShouldFail_ForNonSpecificUser()
        {
            var commandProperties = new BaseCommandProperties
            {
                MinimumRank = Rank.Viewer,
                SpecificUserOnly = "specificuser"
            };
            var eventArgs = new CommandEventArgs
            {
                Name = "testuser"
            };

            var result = await _commandHandler.CheckPermission(commandProperties, eventArgs);

            Assert.False(result);
        }

        [Fact]
        public async Task CheckPermission_ShouldPass_ForSpecificUser()
        {
            var commandProperties = new BaseCommandProperties
            {
                MinimumRank = Rank.Viewer,
                SpecificUserOnly = "specificuser"
            };
            var eventArgs = new CommandEventArgs
            {
                Name = "specificuser"
            };

            var result = await _commandHandler.CheckPermission(commandProperties, eventArgs);

            Assert.True(result);
        }

        [Fact]
        public async Task CheckPermission_ShouldFail_ForNonSpecificRank()
        {
            var commandProperties = new BaseCommandProperties
            {
                MinimumRank = Rank.Viewer,
                SpecificRanks = new List<Rank> { Rank.Moderator }
            };
            var eventArgs = new CommandEventArgs
            {
                Name = "testuser",
                IsMod = false
            };

            var result = await _commandHandler.CheckPermission(commandProperties, eventArgs);

            Assert.False(result);
        }

        [Fact]
        public async Task CheckPermission_ShouldPass_ForSpecificRank()
        {
            var commandProperties = new BaseCommandProperties
            {
                MinimumRank = Rank.Viewer,
                SpecificRanks = new List<Rank> { Rank.Moderator }
            };
            var eventArgs = new CommandEventArgs
            {
                Name = "testuser",
                IsMod = true
            };

            var result = await _commandHandler.CheckPermission(commandProperties, eventArgs);

            Assert.True(result);
        }

        [Fact]
        public async Task CheckPermission_ShouldPass_ForSpecificUsers()
        {
            var commandProperties = new BaseCommandProperties
            {
                MinimumRank = Rank.Viewer,
                SpecificUsersOnly = new List<string> { "specificuser1", "specificuser2" }
            };
            var eventArgs = new CommandEventArgs
            {
                Name = "specificuser1"
            };
            var result = await _commandHandler.CheckPermission(commandProperties, eventArgs);
            Assert.True(result);
        }

        [Fact]
        public async Task CheckPermission_ShouldFail_ForSpecificUsers()
        {
            var commandProperties = new BaseCommandProperties
            {
                MinimumRank = Rank.Viewer,
                SpecificUsersOnly = new List<string> { "specificuser1", "specificuser2" }
            };
            var eventArgs = new CommandEventArgs
            {
                Name = "testuser"
            };
            var result = await _commandHandler.CheckPermission(commandProperties, eventArgs);
            Assert.False(result);
        }
    }
}
