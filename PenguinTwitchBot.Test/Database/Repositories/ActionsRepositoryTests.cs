using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Repository.Repositories;

namespace PenguinTwitchBot.Test.Database.Repositories
{
    public class ActionsRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly ActionsRepository _repository;

        public ActionsRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();
            _repository = new ActionsRepository(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        #region GetByIdWithDetailsAsync

        [Fact]
        public async Task GetByIdWithDetailsAsync_ReturnsActionWithDetails_WhenFound()
        {
            var actionId = Guid.CreateVersion7();
            var action = new ActionType
            {
                Id = actionId,
                Name = "TestAction",
                SubActions = [
                    new SendMessageType { Id = Guid.CreateVersion7(), Index = 1, SubActionTypes = SubActionTypes.SendMessage, Text = "Hello" },
                    new SendMessageType { Id = Guid.CreateVersion7(), Index = 2, SubActionTypes = SubActionTypes.SendMessage, Text = "World" }
                ],
                Triggers = [
                    new TriggerType { Id = Guid.CreateVersion7(), Name = "testcommand", Type = TriggerTypes.Command, Enabled = true, ActionId = actionId }
                ]
            };
            _context.Actions.Add(action);
            await _context.SaveChangesAsync();

            var result = await _repository.GetByIdWithDetailsAsync(actionId);

            Assert.NotNull(result);
            Assert.Equal("TestAction", result!.Name);
            Assert.Equal(2, result.SubActions.Count);
            Assert.Single(result.Triggers);
        }

        [Fact]
        public async Task GetByIdWithDetailsAsync_ReturnsNull_WhenNotFound()
        {
            var result = await _repository.GetByIdWithDetailsAsync(Guid.NewGuid());
            Assert.Null(result);
        }

        #endregion

        #region GetAllWithDetailsAsync

        [Fact]
        public async Task GetAllWithDetailsAsync_ReturnsAllActionsOrderedByName()
        {
            _context.Actions.AddRange(
                new ActionType { Id = Guid.CreateVersion7(), Name = "ZebraAction" },
                new ActionType { Id = Guid.CreateVersion7(), Name = "AlphaAction" },
                new ActionType { Id = Guid.CreateVersion7(), Name = "BetaAction" }
            );
            await _context.SaveChangesAsync();

            var result = await _repository.GetAllWithDetailsAsync();

            Assert.Equal(3, result.Count);
            Assert.Equal("AlphaAction", result[0].Name);
            Assert.Equal("BetaAction", result[1].Name);
            Assert.Equal("ZebraAction", result[2].Name);
        }

        [Fact]
        public async Task GetAllWithDetailsAsync_ReturnsEmptyList_WhenNoActions()
        {
            var result = await _repository.GetAllWithDetailsAsync();
            Assert.Empty(result);
        }

        #endregion

        #region CreateActionAsync

        [Fact]
        public async Task CreateActionAsync_CreatesActionWithSubActions()
        {
            var action = new ActionType
            {
                Name = "NewAction",
                SubActions = [
                    new SendMessageType { Index = 1, SubActionTypes = SubActionTypes.SendMessage, Text = "Hello" }
                ]
            };

            var result = await _repository.CreateActionAsync(action);

            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.NotEqual(Guid.Empty, action.SubActions[0].Id);
            Assert.Single(_context.Actions);
        }

        [Fact]
        public async Task CreateActionAsync_AssignsIdsToSubActions_WhenIdIsEmpty()
        {
            var action = new ActionType
            {
                Name = "ActionWithSubActions",
                SubActions = [
                    new SendMessageType { Id = Guid.Empty, Index = 1, SubActionTypes = SubActionTypes.SendMessage, Text = "Message 1" },
                    new SendMessageType { Id = Guid.Empty, Index = 2, SubActionTypes = SubActionTypes.SendMessage, Text = "Message 2" }
                ]
            };

            await _repository.CreateActionAsync(action);

            Assert.Equal(2, action.SubActions.Count);
            Assert.NotEqual(Guid.Empty, action.SubActions[0].Id);
            Assert.NotEqual(Guid.Empty, action.SubActions[1].Id);
            Assert.NotEqual(action.SubActions[0].Id, action.SubActions[1].Id);
        }

        #endregion

        #region UpdateActionAsync

        [Fact]
        public async Task UpdateActionAsync_UpdatesAction_WhenValidId()
        {
            var actionId = Guid.CreateVersion7();
            var subActionId = Guid.CreateVersion7();
            var existingAction = new ActionType
            {
                Id = actionId,
                Name = "OldName",
                SubActions = [
                    new SendMessageType { Id = subActionId, Index = 1, SubActionTypes = SubActionTypes.SendMessage, Text = "Old" }
                ]
            };
            _context.Actions.Add(existingAction);
            await _context.SaveChangesAsync();

            var updatedAction = new ActionType
            {
                Id = actionId,
                Name = "NewName",
                SubActions = [
                    new SendMessageType { Id = subActionId, Index = 1, SubActionTypes = SubActionTypes.SendMessage, Text = "Updated" }
                ]
            };

            var result = await _repository.UpdateActionAsync(updatedAction);

            Assert.Equal("NewName", result.Name);
            var saved = await _context.Actions.FindAsync(actionId);
            Assert.Equal("NewName", saved!.Name);
        }

        [Fact]
        public async Task UpdateActionAsync_Throws_WhenIdIsEmpty()
        {
            var action = new ActionType { Id = Guid.Empty, Name = "TestAction" };
            await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.UpdateActionAsync(action));
        }

        [Fact]
        public async Task UpdateActionAsync_Throws_WhenActionNotFound()
        {
            var action = new ActionType { Id = Guid.NewGuid(), Name = "NonExistent" };
            await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.UpdateActionAsync(action));
        }

        [Fact]
        public async Task UpdateActionAsync_AddsNewSubActions_WhenNotExisting()
        {
            var actionId = Guid.CreateVersion7();
            var existingAction = new ActionType
            {
                Id = actionId,
                Name = "Action",
                SubActions = []
            };
            _context.Actions.Add(existingAction);
            await _context.SaveChangesAsync();

            var updatedAction = new ActionType
            {
                Id = actionId,
                Name = "Action",
                SubActions = [
                    new SendMessageType { Id = Guid.Empty, Index = 1, SubActionTypes = SubActionTypes.SendMessage, Text = "New" }
                ]
            };

            var result = await _repository.UpdateActionAsync(updatedAction);
            Assert.Single(result.SubActions);
        }

        [Fact]
        public async Task UpdateActionAsync_RemovesDeletedSubActions()
        {
            var actionId = Guid.CreateVersion7();
            var subActionId1 = Guid.CreateVersion7();
            var subActionId2 = Guid.CreateVersion7();

            var existingAction = new ActionType
            {
                Id = actionId,
                Name = "Action",
                SubActions = [
                    new SendMessageType { Id = subActionId1, Index = 1, SubActionTypes = SubActionTypes.SendMessage, Text = "RemoveThis" },
                    new SendMessageType { Id = subActionId2, Index = 2, SubActionTypes = SubActionTypes.SendMessage, Text = "KeepThis" }
                ]
            };
            _context.Actions.Add(existingAction);
            await _context.SaveChangesAsync();

            var updatedAction = new ActionType
            {
                Id = actionId,
                Name = "Action",
                SubActions = [
                    new SendMessageType { Id = subActionId2, Index = 2, SubActionTypes = SubActionTypes.SendMessage, Text = "KeepThis" }
                ]
            };

            await _repository.UpdateActionAsync(updatedAction);

            var saved = await _context.Actions
                .Include(a => a.SubActions)
                .FirstAsync();
            Assert.Single(saved.SubActions);
        }

        #endregion

        #region DeleteActionAsync

        [Fact]
        public async Task DeleteActionAsync_DeletesAction_WhenExists()
        {
            var actionId = Guid.CreateVersion7();
            var action = new ActionType { Id = actionId, Name = "ToDelete" };
            _context.Actions.Add(action);
            await _context.SaveChangesAsync();

            await _repository.DeleteActionAsync(actionId);
            Assert.Null(await _context.Actions.FindAsync(actionId));
        }

        [Fact]
        public async Task DeleteActionAsync_DoesNothing_WhenNotExists()
        {
            await _repository.DeleteActionAsync(Guid.NewGuid());
            Assert.Empty(_context.Actions);
        }

        #endregion

        #region GetActionsByTriggerTypeAndNameAsync

        [Fact]
        public async Task GetActionsByTriggerTypeAndNameAsync_ReturnsMatchingActions()
        {
            var action1Id = Guid.CreateVersion7();
            var action2Id = Guid.CreateVersion7();
            var trigger1 = new TriggerType { Id = Guid.CreateVersion7(), Name = "!test", Type = TriggerTypes.Command, Enabled = true, ActionId = action1Id };
            var trigger2 = new TriggerType { Id = Guid.CreateVersion7(), Name = "!other", Type = TriggerTypes.Command, Enabled = true, ActionId = action2Id };
            var action1 = new ActionType { Id = action1Id, Name = "Action1", Triggers = [trigger1] };
            var action2 = new ActionType { Id = action2Id, Name = "Action2", Triggers = [trigger2] };
            _context.Actions.AddRange(action1, action2);
            await _context.SaveChangesAsync();

            var result = await _repository.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Command, "!test");

            Assert.Single(result);
            Assert.Equal("Action1", result[0].Name);
        }

        [Fact]
        public async Task GetActionsByTriggerTypeAndNameAsync_ReturnsEmptyList_WhenNoMatch()
        {
            var result = await _repository.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Command, "!nonexistent");
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetActionsByTriggerTypeAndNameAsync_ExcludesDisabledTriggers()
        {
            var actionId = Guid.CreateVersion7();
            var trigger = new TriggerType { Id = Guid.CreateVersion7(), Name = "!test", Type = TriggerTypes.Command, Enabled = false, ActionId = actionId };
            var action = new ActionType { Id = actionId, Name = "Action1", Triggers = [trigger] };
            _context.Actions.Add(action);
            await _context.SaveChangesAsync();

            var result = await _repository.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Command, "!test");
            Assert.Empty(result);
        }

        #endregion

        #region GetActionsByCommandIdAsync / GetActionsByKeywordIdAsync

        private async Task<(Guid ActionId, TriggerType Trigger)> AddActionWithTriggerAsync(
            string actionName, TriggerTypes type, string triggerName, Guid? commandId = null, Guid? keywordId = null, bool enabled = true)
        {
            var actionId = Guid.CreateVersion7();
            var trigger = new TriggerType
            {
                Id = Guid.CreateVersion7(),
                Name = triggerName,
                Type = type,
                Enabled = enabled,
                ActionId = actionId,
                CommandId = commandId,
                KeywordId = keywordId
            };
            _context.Actions.Add(new ActionType { Id = actionId, Name = actionName, Triggers = [trigger] });
            await _context.SaveChangesAsync();
            return (actionId, trigger);
        }

        private async Task<Guid> AddCommandAsync(string name)
        {
            var command = new ActionCommand { Id = Guid.CreateVersion7(), CommandName = name };
            _context.ActionCommands.Add(command);
            await _context.SaveChangesAsync();
            return command.Id;
        }

        private async Task<Guid> AddKeywordAsync(string pattern)
        {
            var keyword = new ActionKeyword { Id = Guid.CreateVersion7(), CommandName = pattern };
            _context.ActionKeywords.Add(keyword);
            await _context.SaveChangesAsync();
            return keyword.Id;
        }

        [Fact]
        public async Task GetActionsByCommandIdAsync_MatchesById_EvenWhenTriggerNameIsStale()
        {
            var commandId = await AddCommandAsync("renamed");
            await AddActionWithTriggerAsync("Action1", TriggerTypes.Command, "!oldname", commandId: commandId);

            var result = await _repository.GetActionsByCommandIdAsync(commandId);

            Assert.Single(result);
            Assert.Equal("Action1", result[0].Name);
        }

        [Fact]
        public async Task GetActionsByCommandIdAsync_DoesNotMatchOtherCommand_WithTheSameName()
        {
            var commandId = await AddCommandAsync("first");
            var otherCommandId = await AddCommandAsync("second");
            await AddActionWithTriggerAsync("Action1", TriggerTypes.Command, "!shared", commandId: otherCommandId);

            var result = await _repository.GetActionsByCommandIdAsync(commandId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetActionsByCommandIdAsync_DoesNotMatchTriggersWithoutAnId()
        {
            var commandId = await AddCommandAsync("legacy");
            await AddActionWithTriggerAsync("Action1", TriggerTypes.Command, "!legacy");

            var result = await _repository.GetActionsByCommandIdAsync(commandId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetActionsByCommandIdAsync_ExcludesDisabledTriggers()
        {
            var commandId = await AddCommandAsync("off");
            await AddActionWithTriggerAsync("Action1", TriggerTypes.Command, "!off", commandId: commandId, enabled: false);

            var result = await _repository.GetActionsByCommandIdAsync(commandId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetActionsByKeywordIdAsync_MatchesById_EvenWhenTriggerNameIsStale()
        {
            var keywordId = await AddKeywordAsync("hello");
            await AddActionWithTriggerAsync("Action1", TriggerTypes.Keyword, "hi", keywordId: keywordId);

            var result = await _repository.GetActionsByKeywordIdAsync(keywordId);

            Assert.Single(result);
        }

        [Fact]
        public async Task GetActionsByKeywordIdAsync_DoesNotMatchCommandTriggers()
        {
            var keywordId = await AddKeywordAsync("hello");
            await AddActionWithTriggerAsync("Action1", TriggerTypes.Command, "hello");

            var result = await _repository.GetActionsByKeywordIdAsync(keywordId);

            Assert.Empty(result);
        }

        #endregion

        #region UpdateToggleCommandDisabledNamesForRenamedCommand

        [Fact]
        public async Task UpdateToggleCommandDisabledNamesForRenamedCommand_UpdatesMatchingSubActions()
        {
            var action = new ActionType
            {
                Id = Guid.CreateVersion7(),
                Name = "Action1",
                SubActions = [
                    new ToggleCommandDisabledType { Id = Guid.CreateVersion7(), CommandName = "OldCmd" }
                ]
            };
            _context.Actions.Add(action);
            await _context.SaveChangesAsync();

            await _repository.UpdateToggleCommandDisabledNamesForRenamedCommand("OldCmd", "NewCmd");

            var saved = await _context.SubActions.OfType<ToggleCommandDisabledType>().FirstAsync();
            Assert.Equal("NewCmd", saved.CommandName);
        }

        #endregion
    }
}
