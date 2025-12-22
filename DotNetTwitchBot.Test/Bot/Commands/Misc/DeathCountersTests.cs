using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Misc;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Commands.Misc
{
    public class DeathCountersTests
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly IUnitOfWork dbContext;
        private readonly IServiceProvider serviceProvider;
        private readonly IServiceScope scope;
        private readonly IServiceBackbone serviceBackbone;
        private readonly ITwitchService twitchService;
        private readonly ILogger<DeathCounters> logger;
        private readonly ICommandHandler commandHandler;
        private readonly IViewerFeature viewerFeature;
        private readonly IMediator mediatorSubstitute;
        private readonly DeathCounter testCounter;
        private readonly IQueryable<DeathCounter> counterQueryable;
        private readonly DeathCounters deathCounters;

        public DeathCountersTests()
        {
            scopeFactory = Substitute.For<IServiceScopeFactory>();
            dbContext = Substitute.For<IUnitOfWork>();
            serviceProvider = Substitute.For<IServiceProvider>();
            scope = Substitute.For<IServiceScope>();
            serviceBackbone = Substitute.For<IServiceBackbone>();
            twitchService = Substitute.For<ITwitchService>();
            logger = Substitute.For<ILogger<DeathCounters>>();
            commandHandler = Substitute.For<ICommandHandler>();
            viewerFeature = Substitute.For<IViewerFeature>();
            mediatorSubstitute = Substitute.For<IMediator>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            testCounter = new DeathCounter { Amount = 10, Game = "Star Citizen" };
            counterQueryable = new List<DeathCounter> { testCounter }.BuildMockDbSet().AsQueryable();

            deathCounters = new DeathCounters(twitchService, logger, serviceBackbone, viewerFeature, scopeFactory, mediatorSubstitute, commandHandler);


            commandHandler.GetCommandDefaultName("death").Returns("death");
            viewerFeature.GetDisplayNameByUsername(Arg.Any<string>()).Returns("TheStreamer");
        }


        [Fact]
        public async Task OnCommand_NoGame_ShowThrow()
        {
            // Arrange
            var commandEventArgs = new CommandEventArgs { Command = "death" };

            // Act
            // Assert
            await Assert.ThrowsAsync<SkipCooldownException>(async () => await deathCounters.OnCommand(new object(), commandEventArgs));
        }

        [Fact]
        public async Task OnCommand_ShouldGetCount()
        {
            // Arrange
            var commandEventArgs = new CommandEventArgs { Command = "death" };
            twitchService.GetCurrentGame().Returns("Star Citizen");
            dbContext.DeathCounters.Find(x => true).ReturnsForAnyArgs(counterQueryable);

            // Act
            await deathCounters.OnCommand(new object(), commandEventArgs);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage("TheStreamer has died 10 times in Star Citizen");

        }

        [Fact]
        public async Task OnCommand_ShouldGetCount_NoDeath()
        {
            // Arrange
            var commandEventArgs = new CommandEventArgs { Command = "death" };
            twitchService.GetCurrentGame().Returns("Star Citizen");
            dbContext.DeathCounters.Find(x => true).ReturnsForAnyArgs(counterQueryable);
            testCounter.Amount = 0;

            // Act
            await deathCounters.OnCommand(new object(), commandEventArgs);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage("TheStreamer has not died in Star Citizen YET");

        }

        [Fact]
        public async Task OnCommand_ShouldIncrease()
        {
            // Arrange
            var commandEventArgs = new CommandEventArgs { Command = "death", Args = new List<string> { "+" }, IsMod = true };
            twitchService.GetCurrentGame().Returns("Star Citizen");
            dbContext.DeathCounters.Find(x => true).ReturnsForAnyArgs(counterQueryable);

            // Act
            await deathCounters.OnCommand(new object(), commandEventArgs);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage("TheStreamer has died 11 times in Star Citizen");

        }

        [Fact]
        public async Task OnCommand_ShouldDecrease()
        {
            // Arrange
            var commandEventArgs = new CommandEventArgs { Command = "death", Args = new List<string> { "-" }, IsMod = true };
            twitchService.GetCurrentGame().Returns("Star Citizen");
            dbContext.DeathCounters.Find(x => true).ReturnsForAnyArgs(counterQueryable);

            // Act
            await deathCounters.OnCommand(new object(), commandEventArgs);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage("TheStreamer has died 9 times in Star Citizen");

        }

        [Fact]
        public async Task OnCommand_ShouldReset()
        {
            // Arrange
            var commandEventArgs = new CommandEventArgs { Command = "death", Args = new List<string> { "reset" }, IsMod = true };
            twitchService.GetCurrentGame().Returns("Star Citizen");
            dbContext.DeathCounters.Find(x => true).ReturnsForAnyArgs(counterQueryable);

            // Act
            await deathCounters.OnCommand(new object(), commandEventArgs);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage("TheStreamer has not died in Star Citizen YET");

        }

        [Fact]
        public async Task OnCommand_ShouldSet()
        {
            // Arrange
            var commandEventArgs = new CommandEventArgs { Command = "death", Args = new List<string> { "set", "100" }, IsMod = true };
            twitchService.GetCurrentGame().Returns("Star Citizen");
            dbContext.DeathCounters.Find(x => true).ReturnsForAnyArgs(counterQueryable);

            // Act
            await deathCounters.OnCommand(new object(), commandEventArgs);

            // Assert
            await serviceBackbone.Received(1).SendChatMessage("TheStreamer has died 100 times in Star Citizen");

        }
    }
}
