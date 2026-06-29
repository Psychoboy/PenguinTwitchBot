using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Repository;
using NSubstitute;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Actions
{
    public class RaffleSetupServiceTests
    {
        private RaffleSetupService CreateService(
            IUnitOfWork? unitOfWork = null,
            IActionCommandService? actionCommandService = null,
            IPointsSystem? pointsSystem = null)
        {
            return new RaffleSetupService(
                unitOfWork ?? Substitute.For<IUnitOfWork>(),
                actionCommandService ?? Substitute.For<IActionCommandService>(),
                pointsSystem ?? Substitute.For<IPointsSystem>(),
                Substitute.For<ILogger<RaffleSetupService>>());
        }

        private (IUnitOfWork, IActionsRepository) CreateUnitOfWork()
        {
            var actionsRepo = Substitute.For<IActionsRepository>();
            var actionCommandsRepo = Substitute.For<IActionCommandsRepository>();
            var unitOfWork = Substitute.For<IUnitOfWork>();
            unitOfWork.Actions.Returns(actionsRepo);
            unitOfWork.ActionCommands.Returns(actionCommandsRepo);
            var transaction = Substitute.For<IDbContextTransaction>();
            transaction.CommitAsync().Returns(Task.CompletedTask);
            unitOfWork.BeginTransactionAsync().Returns(transaction);
            unitOfWork.SaveChangesAsync().Returns(1);
            return (unitOfWork, actionsRepo);
        }

        [Fact]
        public async Task CreateAsync_NullRequest_ReturnsFailure()
        {
            var service = CreateService();
            
            var result = await service.CreateAsync(null!);
            
            Assert.False(result.Success);
            Assert.Contains("Request is required.", result.Errors);
        }

        [Fact]
        public async Task CreateAsync_MissingRaffleName_ReturnsFailure()
        {
            var (unitOfWork, actionsRepo) = CreateUnitOfWork();
            var actionCommandService = Substitute.For<IActionCommandService>();
            actionCommandService.CommandExistsAsync(Arg.Any<string>()).Returns(false);
            actionsRepo.GetAllWithDetailsAsync().Returns([]);
            
            var service = CreateService(unitOfWork, actionCommandService);
            
            var request = new RaffleSetupRequest { RaffleKey = "test" };
            var result = await service.CreateAsync(request);
            
            Assert.False(result.Success);
            Assert.Contains("Raffle name is required.", result.Errors);
        }

        [Fact]
        public async Task CreateAsync_MissingRaffleKey_ReturnsFailure()
        {
            var (unitOfWork, actionsRepo) = CreateUnitOfWork();
            var actionCommandService = Substitute.For<IActionCommandService>();
            actionCommandService.CommandExistsAsync(Arg.Any<string>()).Returns(false);
            actionsRepo.GetAllWithDetailsAsync().Returns([]);
            
            var service = CreateService(unitOfWork, actionCommandService);
            
            var request = new RaffleSetupRequest { RaffleName = "Test Raffle" };
            var result = await service.CreateAsync(request);
            
            Assert.False(result.Success);
            Assert.Contains("Raffle key is required.", result.Errors);
        }

        [Fact]
        public async Task CreateAsync_MissingJoinCommand_ReturnsFailure()
        {
            var (unitOfWork, actionsRepo) = CreateUnitOfWork();
            var actionCommandService = Substitute.For<IActionCommandService>();
            actionCommandService.CommandExistsAsync(Arg.Any<string>()).Returns(false);
            actionsRepo.GetAllWithDetailsAsync().Returns([]);
            
            var service = CreateService(unitOfWork, actionCommandService);
            
            var request = new RaffleSetupRequest { RaffleName = "Test", RaffleKey = "test" };
            var result = await service.CreateAsync(request);
            
            Assert.False(result.Success);
            Assert.Contains("Join command is required.", result.Errors);
        }

        [Fact]
        public async Task CreateAsync_MissingStartCommand_ReturnsFailure()
        {
            var (unitOfWork, actionsRepo) = CreateUnitOfWork();
            var actionCommandService = Substitute.For<IActionCommandService>();
            actionCommandService.CommandExistsAsync(Arg.Any<string>()).Returns(false);
            actionsRepo.GetAllWithDetailsAsync().Returns([]);
            
            var service = CreateService(unitOfWork, actionCommandService);
            
            var request = new RaffleSetupRequest 
            { 
                RaffleName = "Test", 
                RaffleKey = "test", 
                JoinCommandName = "join" 
            };
            var result = await service.CreateAsync(request);
            
            Assert.False(result.Success);
            Assert.Contains("Start command is required.", result.Errors);
        }

        [Fact]
        public async Task CreateAsync_MissingEndCommand_ReturnsFailure()
        {
            var (unitOfWork, actionsRepo) = CreateUnitOfWork();
            var actionCommandService = Substitute.For<IActionCommandService>();
            actionCommandService.CommandExistsAsync(Arg.Any<string>()).Returns(false);
            actionsRepo.GetAllWithDetailsAsync().Returns([]);
            
            var service = CreateService(unitOfWork, actionCommandService);
            
            var request = new RaffleSetupRequest 
            { 
                RaffleName = "Test", 
                RaffleKey = "test", 
                JoinCommandName = "join",
                StartCommandName = "start"
            };
            var result = await service.CreateAsync(request);
            
            Assert.False(result.Success);
            Assert.Contains("End command is required.", result.Errors);
        }

        [Fact]
        public async Task CreateAsync_DuplicateCommands_ReturnsFailure()
        {
            var (unitOfWork, actionsRepo) = CreateUnitOfWork();
            var actionCommandService = Substitute.For<IActionCommandService>();
            actionCommandService.CommandExistsAsync("join").Returns(true);
            actionsRepo.GetAllWithDetailsAsync().Returns([]);
            
            var service = CreateService(unitOfWork, actionCommandService);
            
            var request = new RaffleSetupRequest 
            { 
                RaffleName = "Test", 
                RaffleKey = "test", 
                JoinCommandName = "join",
                StartCommandName = "start",
                EndCommandName = "end",
                WinnerCount = 1,
                TotalAward = 0
            };
            var result = await service.CreateAsync(request);
            
            Assert.False(result.Success);
            Assert.Contains("already exists", result.Errors.FirstOrDefault()!);
        }

        [Fact]
        public async Task CreateAsync_DuplicateCommands_AllSame_ReturnsFailure()
        {
            var (unitOfWork, actionsRepo) = CreateUnitOfWork();
            var actionCommandService = Substitute.For<IActionCommandService>();
            actionCommandService.CommandExistsAsync(Arg.Any<string>()).Returns(false);
            actionsRepo.GetAllWithDetailsAsync().Returns([]);
            
            var service = CreateService(unitOfWork, actionCommandService);
            
            var request = new RaffleSetupRequest 
            { 
                RaffleName = "Test", 
                RaffleKey = "test", 
                JoinCommandName = "join",
                StartCommandName = "JOIN",
                EndCommandName = "end",
                WinnerCount = 1,
                TotalAward = 0
            };
            var result = await service.CreateAsync(request);
            
            Assert.False(result.Success);
            Assert.Contains("must all be different", result.Errors.FirstOrDefault()!);
        }

        [Fact]
        public async Task CreateAsync_ExistingRaffleKey_ReturnsFailure()
        {
            var (unitOfWork, actionsRepo) = CreateUnitOfWork();
            var actionCommandService = Substitute.For<IActionCommandService>();
            actionCommandService.CommandExistsAsync(Arg.Any<string>()).Returns(false);
            var existingAction = new ActionType
            {
                Id = 1,
                Name = "Existing",
                SubActions = [
                    new RaffleStartType { RaffleKey = "existing-key" }
                ]
            };
            actionsRepo.GetAllWithDetailsAsync().Returns([existingAction]);
            
            var service = CreateService(unitOfWork, actionCommandService);
            
            var request = new RaffleSetupRequest 
            { 
                RaffleName = "Test", 
                RaffleKey = "existing-key", 
                JoinCommandName = "join",
                StartCommandName = "start",
                EndCommandName = "end",
                WinnerCount = 1,
                TotalAward = 0
            };
            var result = await service.CreateAsync(request);
            
            Assert.False(result.Success);
            Assert.Contains("already used", result.Errors.FirstOrDefault()!);
        }

        [Fact]
        public async Task CreateAsync_ThrowsException_ReturnsFailure()
        {
            var (unitOfWork, actionsRepo) = CreateUnitOfWork();
            var actionCommandService = Substitute.For<IActionCommandService>();
            actionCommandService.CommandExistsAsync(Arg.Any<string>()).Returns(false);
            actionsRepo.GetAllWithDetailsAsync().Returns([]);
            actionsRepo.When(x => x.GetAllWithDetailsAsync())
                .Do(_ => throw new InvalidOperationException("Database error"));
            
            var service = CreateService(unitOfWork, actionCommandService);
            
            var request = new RaffleSetupRequest 
            { 
                RaffleName = "Test", 
                RaffleKey = "test", 
                JoinCommandName = "join",
                StartCommandName = "start",
                EndCommandName = "end",
                WinnerCount = 1,
                TotalAward = 0
            };
            var result = await service.CreateAsync(request);
            
            Assert.False(result.Success);
            Assert.Contains("An error occurred while creating", result.Errors.FirstOrDefault()!);
        }
    }
}