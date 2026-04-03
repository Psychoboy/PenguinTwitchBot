using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Models.Actions.Triggers;
using DotNetTwitchBot.Bot.Models.Commands;
using DotNetTwitchBot.Repository;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json;

namespace DotNetTwitchBot.Test.Bot.Commands
{
    public class ActionCommandServiceTests
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICommandHelper _commandHelper;
        private readonly IActionManagementService _actionManagementService;
        private readonly ILogger<ActionCommandService> _logger;
        private readonly ActionCommandService _service;

        public ActionCommandServiceTests()
        {
            _unitOfWork = Substitute.For<IUnitOfWork>();
            _commandHelper = Substitute.For<ICommandHelper>();
            _actionManagementService = Substitute.For<IActionManagementService>();
            _logger = Substitute.For<ILogger<ActionCommandService>>();
            _service = new ActionCommandService(_unitOfWork, _commandHelper, _actionManagementService, _logger);
        }

        [Fact]
        public async Task DeleteAsync_ShouldDeleteCommandTriggersBeforeDeletingCommand()
        {
            // Arrange
            var commandId = 1;
            var command = new ActionCommand
            {
                Id = commandId,
                CommandName = "testcommand"
            };

            _unitOfWork.ActionCommands.GetByIdAsync(commandId).Returns(command);
            _unitOfWork.SaveChangesAsync().Returns(Task.FromResult(1));
            _actionManagementService.DeleteTriggersForCommandAsync(commandId).Returns(Task.CompletedTask);

            // Act
            await _service.DeleteAsync(commandId);

            // Assert
            await _actionManagementService.Received(1).DeleteTriggersForCommandAsync(commandId);
            _unitOfWork.ActionCommands.Received(1).Remove(command);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task DeleteAsync_ShouldDeleteCommandEvenIfTriggerDeletionFails()
        {
            // Arrange
            var commandId = 1;
            var command = new ActionCommand
            {
                Id = commandId,
                CommandName = "testcommand"
            };

            _unitOfWork.ActionCommands.GetByIdAsync(commandId).Returns(command);
            _unitOfWork.SaveChangesAsync().Returns(Task.FromResult(1));
            _actionManagementService.DeleteTriggersForCommandAsync(commandId)
                .Returns(Task.FromException(new Exception("Database error")));

            // Act
            await _service.DeleteAsync(commandId);

            // Assert
            await _actionManagementService.Received(1).DeleteTriggersForCommandAsync(commandId);
            _unitOfWork.ActionCommands.Received(1).Remove(command);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task DeleteAsync_ShouldNotDeleteWhenCommandNotFound()
        {
            // Arrange
            var commandId = 1;
            _unitOfWork.ActionCommands.GetByIdAsync(commandId).Returns((ActionCommand?)null);

            // Act
            await _service.DeleteAsync(commandId);

            // Assert
            await _actionManagementService.DidNotReceive().DeleteTriggersForCommandAsync(Arg.Any<int>());
            _unitOfWork.ActionCommands.DidNotReceive().Remove(Arg.Any<ActionCommand>());
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }
    }
}
