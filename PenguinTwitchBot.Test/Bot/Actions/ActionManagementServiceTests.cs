using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Test.Bot.Actions
{
    public class ActionManagementServiceTests
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ActionManagementService> _logger;
        private readonly ActionManagementService _service;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IActionsRepository _actionsRepo;
        private readonly ITriggersRepository _triggersRepo;
        private readonly IServiceScope _scope;

        public ActionManagementServiceTests()
        {
            _scopeFactory = Substitute.For<IServiceScopeFactory>();
            _logger = Substitute.For<ILogger<ActionManagementService>>();
            _unitOfWork = Substitute.For<IUnitOfWork>();
            _actionsRepo = Substitute.For<IActionsRepository>();
            _triggersRepo = Substitute.For<ITriggersRepository>();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_unitOfWork);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            _scope = serviceProvider.CreateScope();

            #pragma warning disable NS1000
            _scopeFactory.CreateScope().Returns(_scope);
            #pragma warning restore NS1000

            _unitOfWork.Actions.Returns(_actionsRepo);
            _unitOfWork.Triggers.Returns(_triggersRepo);

            _service = new ActionManagementService(_scopeFactory, _logger);
        }

        private SubActionType CreateSubAction(int index)
        {
            return new SendMessageType { Index = index, SubActionTypes = SubActionTypes.SendMessage };
        }

        #region GetAllActionsAsync

        [Fact]
        public async Task GetAllActionsAsync_ReturnsActionsWithOrderedSubActions()
        {
            // Arrange
            var action1 = new ActionType
            {
                Id = Guid.CreateVersion7(),
                Name = "Action1",
                SubActions = [
                    CreateSubAction(2),
                    CreateSubAction(1)
                ]
            };
            var action2 = new ActionType
            {
                Id = Guid.CreateVersion7(),
                Name = "Action2",
                SubActions = []
            };

            _actionsRepo.GetAllWithDetailsAsync().Returns([action1, action2]);

            // Act
            var result = await _service.GetAllActionsAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].SubActions[0].Index);
            Assert.Equal(2, result[0].SubActions[1].Index);
        }

        #endregion

        #region GetActionByIdAsync

        [Fact]
        public async Task GetActionByIdAsync_ReturnsNull_WhenActionNotFound()
        {
            // Arrange
            var actionId = Guid.CreateVersion7();
            _actionsRepo.GetByIdWithDetailsAsync(actionId).Returns((ActionType?)null);

            // Act
            var result = await _service.GetActionByIdAsync(actionId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetActionByIdAsync_ReturnsActionWithOrderedSubActions()
        {
            // Arrange
            var actionId = Guid.CreateVersion7();
            var action = new ActionType
            {
                Id = actionId,
                Name = "TestAction",
                SubActions = [
                    CreateSubAction(3),
                    CreateSubAction(1),
                    CreateSubAction(2)
                ]
            };

            _actionsRepo.GetByIdWithDetailsAsync(actionId).Returns(action);

            // Act
            var result = await _service.GetActionByIdAsync(actionId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result!.SubActions.Count);
            Assert.Equal(1, result.SubActions[0].Index);
            Assert.Equal(2, result.SubActions[1].Index);
            Assert.Equal(3, result.SubActions[2].Index);
        }

        #endregion

        #region CreateActionAsync

        [Fact]
        public async Task CreateActionAsync_ReturnsCreatedAction()
        {
            // Arrange
            var action = new ActionType
            {
                Name = "NewAction",
                SubActions = []
            };
            _actionsRepo.CreateActionAsync(action).Returns(action);

            // Act
            var result = await _service.CreateActionAsync(action);

            // Assert
            Assert.Equal(action, result);
            await _actionsRepo.Received(1).CreateActionAsync(action);
        }

        [Fact]
        public async Task CreateActionAsync_LogsErrorAndThrows_WhenExceptionOccurs()
        {
            // Arrange
            var action = new ActionType { Name = "TestAction" };
            var exception = new Exception("Database error");
            _actionsRepo.When(x => x.CreateActionAsync(action)).Throw(exception);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.CreateActionAsync(action));

            _logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception,
                Arg.Any<Func<object, Exception?, string>>());
        }

        #endregion

        #region UpdateActionAsync

        [Fact]
        public async Task UpdateActionAsync_ReturnsUpdatedAction()
        {
            // Arrange
            var action = new ActionType { Id = Guid.CreateVersion7(), Name = "TestAction" };
            _actionsRepo.UpdateActionAsync(action).Returns(action);

            // Act
            var result = await _service.UpdateActionAsync(action);

            // Assert
            Assert.Equal(action, result);
            await _actionsRepo.Received(1).UpdateActionAsync(action);
        }

        [Fact]
        public async Task UpdateActionAsync_LogsErrorAndThrows_WhenExceptionOccurs()
        {
            // Arrange
            var action = new ActionType { Id = Guid.CreateVersion7(), Name = "TestAction" };
            var exception = new Exception("Database error");
            _actionsRepo.When(x => x.UpdateActionAsync(action)).Throw(exception);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.UpdateActionAsync(action));

            _logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception,
                Arg.Any<Func<object, Exception?, string>>());
        }

        #endregion

        #region DeleteActionAsync

        [Fact]
        public async Task DeleteActionAsync_CallsDeleteActionAsync()
        {
            // Arrange
            var actionId = Guid.CreateVersion7();

            // Act
            await _service.DeleteActionAsync(actionId);

            // Assert
            await _actionsRepo.Received(1).DeleteActionAsync(actionId);
        }

        [Fact]
        public async Task DeleteActionAsync_LogsErrorAndThrows_WhenExceptionOccurs()
        {
            // Arrange
            var actionId = Guid.CreateVersion7();
            var exception = new Exception("Database error");
            _actionsRepo.When(x => x.DeleteActionAsync(actionId)).Throw(exception);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.DeleteActionAsync(actionId));

            _logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception,
                Arg.Any<Func<object, Exception?, string>>());
        }

        #endregion

        #region GetActionsByTriggerTypeAndNameAsync

        [Fact]
        public async Task GetActionsByTriggerTypeAndNameAsync_ReturnsActionsWithOrderedSubActions()
        {
            // Arrange
            var action = new ActionType
            {
                Id = Guid.CreateVersion7(),
                Name = "TestAction",
                SubActions = [
                    CreateSubAction(2),
                    CreateSubAction(1)
                ]
            };

            _actionsRepo.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Command, "testcommand").Returns([action]);

            // Act
            var result = await _service.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Command, "testcommand");

            // Assert
            Assert.Single(result);
            Assert.Equal(1, result[0].SubActions[0].Index);
            Assert.Equal(2, result[0].SubActions[1].Index);
        }

        [Fact]
        public async Task GetActionsByTriggerTypeAndNameAsync_LogsErrorAndThrows_WhenExceptionOccurs()
        {
            // Arrange
            var exception = new Exception("Database error");
            _actionsRepo.When(x => x.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Command, "test"))
                .Throw(exception);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => 
                _service.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Command, "test"));

            _logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception,
                Arg.Any<Func<object, Exception?, string>>());
        }

        #endregion

        #region GetTriggersForActionAsync

        [Fact]
        public async Task GetTriggersForActionAsync_ReturnsTriggers()
        {
            // Arrange
            var actionId = Guid.CreateVersion7();
            var triggers = new List<TriggerType>
            {
                new TriggerType { Id = Guid.CreateVersion7(), ActionId = actionId, Name = "Trigger1" },
                new TriggerType { Id = Guid.CreateVersion7(), ActionId = actionId, Name = "Trigger2" }
            };
            _triggersRepo.GetTriggersForActionAsync(actionId).Returns(triggers);

            // Act
            var result = await _service.GetTriggersForActionAsync(actionId);

            // Assert
            Assert.Equal(2, result.Count);
        }

        #endregion

        #region GetAllTriggersAsync

        [Fact]
        public async Task GetAllTriggersAsync_ReturnsAllTriggers()
        {
            // Arrange
            var triggers = new List<TriggerType>
            {
                new TriggerType { Id = Guid.CreateVersion7(), Name = "Trigger1" },
                new TriggerType { Id = Guid.CreateVersion7(), Name = "Trigger2" }
            };
            _triggersRepo.GetAllAsync().Returns(triggers);

            // Act
            var result = await _service.GetAllTriggersAsync();

            // Assert
            Assert.Equal(2, result.Count);
        }

        #endregion

        #region GetTriggerByIdAsync

        [Fact]
        public async Task GetTriggerByIdAsync_ReturnsTrigger()
        {
            // Arrange
            var triggerId = Guid.CreateVersion7();
            var trigger = new TriggerType { Id = triggerId, Name = "TestTrigger" };
            _triggersRepo.GetByIdAsync(triggerId).Returns(trigger);

            // Act
            var result = await _service.GetTriggerByIdAsync(triggerId);

            // Assert
            Assert.Equal(trigger, result);
        }

        [Fact]
        public async Task GetTriggerByIdAsync_ReturnsNull_WhenTriggerNotFound()
        {
            // Arrange
            var triggerId = Guid.CreateVersion7();
            _triggersRepo.GetByIdAsync(triggerId).Returns((TriggerType?)null);

            // Act
            var result = await _service.GetTriggerByIdAsync(triggerId);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region CreateTriggerAsync

        [Fact]
        public async Task CreateTriggerAsync_ReturnsCreatedTrigger()
        {
            // Arrange
            var trigger = new TriggerType { Name = "NewTrigger" };
            _triggersRepo.AddAsync(trigger).Returns(trigger);

            // Act
            var result = await _service.CreateTriggerAsync(trigger);

            // Assert
            Assert.Equal(trigger, result);
        }

        [Fact]
        public async Task CreateTriggerAsync_LogsErrorAndThrows_WhenExceptionOccurs()
        {
            // Arrange
            var trigger = new TriggerType { Name = "TestTrigger" };
            var exception = new Exception("Database error");
            _triggersRepo.When(x => x.AddAsync(trigger)).Throw(exception);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.CreateTriggerAsync(trigger));

            _logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception,
                Arg.Any<Func<object, Exception?, string>>());
        }

        #endregion

        #region UpdateTriggerAsync

        [Fact]
        public async Task UpdateTriggerAsync_ReturnsUpdatedTrigger()
        {
            // Arrange
            var trigger = new TriggerType { Id = Guid.CreateVersion7(), Name = "UpdatedTrigger" };
            _triggersRepo.UpdateAsync(trigger).Returns(trigger);

            // Act
            var result = await _service.UpdateTriggerAsync(trigger);

            // Assert
            Assert.Equal(trigger, result);
        }

        [Fact]
        public async Task UpdateTriggerAsync_LogsErrorAndThrows_WhenExceptionOccurs()
        {
            // Arrange
            var trigger = new TriggerType { Id = Guid.CreateVersion7(), Name = "TestTrigger" };
            var exception = new Exception("Database error");
            _triggersRepo.When(x => x.UpdateAsync(trigger)).Throw(exception);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.UpdateTriggerAsync(trigger));

            _logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception,
                Arg.Any<Func<object, Exception?, string>>());
        }

        #endregion

        #region DeleteTriggerAsync

        [Fact]
        public async Task DeleteTriggerAsync_CallsDeleteAsync()
        {
            // Arrange
            var triggerId = Guid.CreateVersion7();

            // Act
            await _service.DeleteTriggerAsync(triggerId);

            // Assert
            await _triggersRepo.Received(1).DeleteAsync(triggerId);
        }

        [Fact]
        public async Task DeleteTriggerAsync_LogsErrorAndThrows_WhenExceptionOccurs()
        {
            // Arrange
            var triggerId = Guid.CreateVersion7();
            var exception = new Exception("Database error");
            _triggersRepo.When(x => x.DeleteAsync(triggerId)).Throw(exception);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.DeleteTriggerAsync(triggerId));

            _logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception,
                Arg.Any<Func<object, Exception?, string>>());
        }

        #endregion

        #region DeleteTriggersForCommandAsync

        [Fact]
        public async Task DeleteTriggersForCommandAsync_DeletesAllTriggersForCommand()
        {
            // Arrange
            var commandId = Guid.CreateVersion7();
            var triggerId1 = Guid.CreateVersion7();
            var triggerId2 = Guid.CreateVersion7();
            var triggerId3 = Guid.CreateVersion7();
            var triggers = new List<TriggerType>
            {
                new TriggerType { Id = triggerId1, CommandId = commandId },
                new TriggerType { Id = triggerId2, CommandId = commandId },
                new TriggerType { Id = triggerId3, CommandId = commandId }
            };
            _triggersRepo.GetByCommandIdAsync(commandId).Returns(triggers);

            // Act
            await _service.DeleteTriggersForCommandAsync(commandId);

            // Assert
            await _triggersRepo.Received(1).GetByCommandIdAsync(commandId);
            await _triggersRepo.Received(1).DeleteAsync(triggerId1);
            await _triggersRepo.Received(1).DeleteAsync(triggerId2);
            await _triggersRepo.Received(1).DeleteAsync(triggerId3);
        }

        [Fact]
        public async Task DeleteTriggersForCommandAsync_DeletesZeroTriggers_WhenNoTriggersExist()
        {
            // Arrange
            var commandId = Guid.CreateVersion7();
            _triggersRepo.GetByCommandIdAsync(commandId).Returns([]);

            // Act
            await _service.DeleteTriggersForCommandAsync(commandId);

            // Assert
            await _triggersRepo.DidNotReceive().DeleteAsync(Arg.Any<Guid>());
        }

        [Fact]
        public async Task DeleteTriggersForCommandAsync_LogsErrorAndThrows_WhenExceptionOccurs()
        {
            // Arrange
            var commandId = Guid.CreateVersion7();
            var exception = new Exception("Database error");
            _triggersRepo.When(x => x.GetByCommandIdAsync(commandId)).Throw(exception);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.DeleteTriggersForCommandAsync(commandId));

            _logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception,
                Arg.Any<Func<object, Exception?, string>>());
        }

        #endregion

        #region DeleteTriggersForKeywordAsync

        [Fact]
        public async Task DeleteTriggersForKeywordAsync_DeletesAllTriggersForKeyword()
        {
            // Arrange
            var keywordId = Guid.CreateVersion7();
            var triggerId1 = Guid.CreateVersion7();
            var triggerId2 = Guid.CreateVersion7();
            var triggers = new List<TriggerType>
            {
                new TriggerType { Id = triggerId1, KeywordId = keywordId },
                new TriggerType { Id = triggerId2, KeywordId = keywordId }
            };
            _triggersRepo.GetByKeywordIdAsync(keywordId).Returns(triggers);

            // Act
            await _service.DeleteTriggersForKeywordAsync(keywordId);

            // Assert
            await _triggersRepo.Received(1).GetByKeywordIdAsync(keywordId);
            await _triggersRepo.Received(1).DeleteAsync(triggerId1);
            await _triggersRepo.Received(1).DeleteAsync(triggerId2);
        }

        [Fact]
        public async Task DeleteTriggersForKeywordAsync_DeletesZeroTriggers_WhenNoTriggersExist()
        {
            // Arrange
            var keywordId = Guid.CreateVersion7();
            _triggersRepo.GetByKeywordIdAsync(keywordId).Returns([]);

            // Act
            await _service.DeleteTriggersForKeywordAsync(keywordId);

            // Assert
            await _triggersRepo.DidNotReceive().DeleteAsync(Arg.Any<Guid>());
        }

        [Fact]
        public async Task DeleteTriggersForKeywordAsync_LogsErrorAndThrows_WhenExceptionOccurs()
        {
            // Arrange
            var keywordId = Guid.CreateVersion7();
            var exception = new Exception("Database error");
            _triggersRepo.When(x => x.GetByKeywordIdAsync(keywordId)).Throw(exception);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.DeleteTriggersForKeywordAsync(keywordId));

            _logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception,
                Arg.Any<Func<object, Exception?, string>>());
        }

        #endregion
    }
}
