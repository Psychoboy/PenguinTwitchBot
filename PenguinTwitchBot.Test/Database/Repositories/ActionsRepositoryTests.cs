using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Timers;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Repository.Repositories;
using System.Text.Json;

namespace PenguinTwitchBot.Test.Database.Repositories
{
    public class ActionsRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly ActionsRepository _repository;
        private readonly ILogger _logger;

        public ActionsRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();
            _logger = Substitute.For<ILogger>();
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
            var action = new ActionType
            {
                Id = 1,
                Name = "TestAction",
                SubActions = [
                    new SendMessageType { Id = 1, Index = 1, SubActionTypes = SubActionTypes.SendMessage, Text = "Hello" },
                    new SendMessageType { Id = 2, Index = 2, SubActionTypes = SubActionTypes.SendMessage, Text = "World" }
                ],
                Triggers = [
                    new TriggerType { Id = 1, Name = "testcommand", Type = TriggerTypes.Command, Enabled = true, ActionId = 1 }
                ]
            };
            _context.Actions.Add(action);
            await _context.SaveChangesAsync();

            var result = await _repository.GetByIdWithDetailsAsync(1);

            Assert.NotNull(result);
            Assert.Equal("TestAction", result!.Name);
            Assert.Equal(2, result.SubActions.Count);
            Assert.Single(result.Triggers);
        }

        [Fact]
        public async Task GetByIdWithDetailsAsync_ReturnsNull_WhenNotFound()
        {
            var result = await _repository.GetByIdWithDetailsAsync(999);
            Assert.Null(result);
        }

        #endregion

        #region GetAllWithDetailsAsync

        [Fact]
        public async Task GetAllWithDetailsAsync_ReturnsAllActionsOrderedByName()
        {
            _context.Actions.AddRange(
                new ActionType { Id = 1, Name = "ZebraAction" },
                new ActionType { Id = 2, Name = "AlphaAction" },
                new ActionType { Id = 3, Name = "BetaAction" }
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
            Assert.NotEqual(0, result.Id);
            Assert.NotEqual(0, action.SubActions[0].Id);
            Assert.Single(_context.Actions);
        }

        [Fact]
        public async Task CreateActionAsync_AssignsIdsToSubActions_WhenIdIsZero()
        {
            var action = new ActionType
            {
                Name = "ActionWithSubActions",
                SubActions = [
                    new SendMessageType { Id = 0, Index = 1, SubActionTypes = SubActionTypes.SendMessage, Text = "Message 1" },
                    new SendMessageType { Id = 0, Index = 2, SubActionTypes = SubActionTypes.SendMessage, Text = "Message 2" }
                ]
            };

            await _repository.CreateActionAsync(action);

            Assert.Equal(2, action.SubActions.Count);
            Assert.NotEqual(0, action.SubActions[0].Id);
            Assert.NotEqual(0, action.SubActions[1].Id);
            Assert.NotEqual(action.SubActions[0].Id, action.SubActions[1].Id);
        }

        [Fact]
        public async Task CreateActionAsync_PopulatesExecuteActionNames_WhenExecuteActionSubActionsExist()
        {
            var referencedAction = new ActionType { Id = 1, Name = "TargetAction" };
            _context.Actions.Add(referencedAction);
            await _context.SaveChangesAsync();

            var action = new ActionType
            {
                Name = "ActionWithExecuteAction",
                SubActions = [
                    new ExecuteActionType { Id = 0, ActionId = 1, ActionName = "" }
                ]
            };

            await _repository.CreateActionAsync(action);

            Assert.Equal("TargetAction", ((ExecuteActionType)action.SubActions[0]).ActionName);
        }

        [Fact]
        public async Task CreateActionAsync_PopulatesTimerGroupNames_WhenTimerGroupSubActionsExist()
        {
            var timerGroup = new TimerGroup { Id = 1, Name = "TestTimerGroup" };
            _context.TimerGroups.Add(timerGroup);
            await _context.SaveChangesAsync();

            var action = new ActionType
            {
                Name = "ActionWithTimerGroup",
                SubActions = [
                    new TimerGroupSetEnabledStateType { Id = 0, TimerGroupId = 1, TimerGroupName = "" }
                ]
            };

            await _repository.CreateActionAsync(action);

            Assert.Equal("TestTimerGroup", ((TimerGroupSetEnabledStateType)action.SubActions[0]).TimerGroupName);
        }

        #endregion

        #region UpdateActionAsync

        [Fact]
        public async Task UpdateActionAsync_UpdatesAction_WhenValidId()
        {
            var existingAction = new ActionType
            {
                Id = 1,
                Name = "OldName",
                SubActions = [
                    new SendMessageType { Id = 1, Index = 1, SubActionTypes = SubActionTypes.SendMessage, Text = "Old" }
                ]
            };
            _context.Actions.Add(existingAction);
            await _context.SaveChangesAsync();

            var updatedAction = new ActionType
            {
                Id = 1,
                Name = "NewName",
                SubActions = [
                    new SendMessageType { Id = 1, Index = 1, SubActionTypes = SubActionTypes.SendMessage, Text = "Updated" }
                ]
            };

            var result = await _repository.UpdateActionAsync(updatedAction);

            Assert.Equal("NewName", result.Name);
            var saved = await _context.Actions.FindAsync(1);
            Assert.Equal("NewName", saved!.Name);
        }

        [Fact]
        public async Task UpdateActionAsync_Throws_WhenIdIsNull()
        {
            var action = new ActionType { Name = "TestAction" };
            await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.UpdateActionAsync(action));
        }

        [Fact]
        public async Task UpdateActionAsync_Throws_WhenIdIsZero()
        {
            var action = new ActionType { Id = 0, Name = "TestAction" };
            await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.UpdateActionAsync(action));
        }

        [Fact]
        public async Task UpdateActionAsync_Throws_WhenActionNotFound()
        {
            var action = new ActionType { Id = 999, Name = "NonExistent" };
            await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.UpdateActionAsync(action));
        }

        [Fact]
        public async Task UpdateActionAsync_AddsNewSubActions_WhenNotExisting()
        {
            var existingAction = new ActionType
            {
                Id = 1,
                Name = "Action",
                SubActions = []
            };
            _context.Actions.Add(existingAction);
            await _context.SaveChangesAsync();

            var updatedAction = new ActionType
            {
                Id = 1,
                Name = "Action",
                SubActions = [
                    new SendMessageType { Id = 0, Index = 1, SubActionTypes = SubActionTypes.SendMessage, Text = "New" }
                ]
            };

            var result = await _repository.UpdateActionAsync(updatedAction);
            Assert.Single(result.SubActions);
        }

        [Fact]
        public async Task UpdateActionAsync_RemovesDeletedSubActions()
        {
            var existingAction = new ActionType
            {
                Id = 1,
                Name = "Action",
                SubActions = [
                    new SendMessageType { Id = 1, Index = 1, SubActionTypes = SubActionTypes.SendMessage, Text = "RemoveThis" },
                    new SendMessageType { Id = 2, Index = 2, SubActionTypes = SubActionTypes.SendMessage, Text = "KeepThis" }
                ]
            };
            _context.Actions.Add(existingAction);
            await _context.SaveChangesAsync();

            var updatedAction = new ActionType
            {
                Id = 1,
                Name = "Action",
                SubActions = [
                    new SendMessageType { Id = 2, Index = 2, SubActionTypes = SubActionTypes.SendMessage, Text = "KeepThis" }
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
            var action = new ActionType { Id = 1, Name = "ToDelete" };
            _context.Actions.Add(action);
            await _context.SaveChangesAsync();

            await _repository.DeleteActionAsync(1);
            Assert.Null(await _context.Actions.FindAsync(1));
        }

        [Fact]
        public async Task DeleteActionAsync_DoesNothing_WhenNotExists()
        {
            await _repository.DeleteActionAsync(999);
            Assert.Empty(_context.Actions);
        }

        #endregion

        #region GetActionsByTriggerTypeAndNameAsync

        [Fact]
        public async Task GetActionsByTriggerTypeAndNameAsync_ReturnsMatchingActions()
        {
            var trigger1 = new TriggerType { Id = 1, Name = "!test", Type = TriggerTypes.Command, Enabled = true, ActionId = 1 };
            var trigger2 = new TriggerType { Id = 2, Name = "!other", Type = TriggerTypes.Command, Enabled = true, ActionId = 2 };
            var action1 = new ActionType { Id = 1, Name = "Action1", Triggers = [trigger1] };
            var action2 = new ActionType { Id = 2, Name = "Action2", Triggers = [trigger2] };
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
            var trigger = new TriggerType { Id = 1, Name = "!test", Type = TriggerTypes.Command, Enabled = false, ActionId = 1 };
            var action = new ActionType { Id = 1, Name = "Action1", Triggers = [trigger] };
            _context.Actions.Add(action);
            await _context.SaveChangesAsync();

            var result = await _repository.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Command, "!test");
            Assert.Empty(result);
        }

        #endregion

        #region BackupTable

        [Fact]
        public async Task BackupTable_WritesJsonFile()
        {
            var action = new ActionType { Id = 1, Name = "TestAction" };
            _context.Actions.Add(action);
            await _context.SaveChangesAsync();
            var backupDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(backupDir);

            try
            {
                await _repository.BackupTable(_context, backupDir, _logger);
                var backupFile = Path.Combine(backupDir, "ActionType.json");
                Assert.True(File.Exists(backupFile));
            }
            finally
            {
                Directory.Delete(backupDir, true);
            }
        }

        #endregion

        #region RestoreTable

        [Fact]
        public async Task RestoreTable_Throws_WhenFileNotFound()
        {
            var backupDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(backupDir);

            try
            {
                await _repository.RestoreTable(_context, backupDir, _logger);
                Assert.Empty(_context.Actions);
            }
            finally
            {
                Directory.Delete(backupDir, true);
            }
        }

        #endregion

        #region UpdateExecuteActionNamesForRenamedAction

        [Fact]
        public async Task UpdateExecuteActionNamesForRenamedAction_UpdatesNames_WhenActionReferenced()
        {
            var action = new ActionType
            {
                Id = 1,
                Name = "Action1",
                SubActions = [
                    new ExecuteActionType { Id = 1, ActionId = 1, ActionName = "OldName" }
                ]
            };
            _context.Actions.Add(action);
            await _context.SaveChangesAsync();

            await _repository.UpdateExecuteActionNamesForRenamedAction(1, "NewName");

            var saved = await _context.SubActions.OfType<ExecuteActionType>().FirstAsync();
            Assert.Equal("NewName", saved.ActionName);
        }

        [Fact]
        public async Task UpdateExecuteActionNamesForRenamedAction_DoesNotSave_WhenNoChanges()
        {
            var action = new ActionType
            {
                Id = 1,
                Name = "Action1",
                SubActions = [
                    new ExecuteActionType { Id = 1, ActionId = 1, ActionName = "AlreadyCorrect" }
                ]
            };
            _context.Actions.Add(action);
            await _context.SaveChangesAsync();

            await _repository.UpdateExecuteActionNamesForRenamedAction(1, "AlreadyCorrect");
        }

        #endregion

        #region UpdateCommandTriggerConfigurationsForRenamedCommand

        [Fact]
        public async Task UpdateCommandTriggerConfigurationsForRenamedCommand_UpdatesTriggers()
        {
            var command = new ActionCommand { Id = 1, CommandName = "test" };
            _context.ActionCommands.Add(command);
            _context.Triggers.Add(new TriggerType
            {
                Id = 1,
                Type = TriggerTypes.Command,
                Name = "!oldcommand",
                Enabled = true,
                Configuration = "{\"CommandId\": 1, \"CommandName\": \"OldCommand\"}"
            });
            await _context.SaveChangesAsync();

            await _repository.UpdateCommandTriggerConfigurationsForRenamedCommand(1, "oldcommand", "newcommand");

            var saved = await _context.Triggers.FirstAsync();
            Assert.Contains("newcommand", saved.Configuration);
        }

        #endregion

        #region UpdateKeywordTriggerConfigurationsForRenamedKeyword

        [Fact]
        public async Task UpdateKeywordTriggerConfigurationsForRenamedKeyword_UpdatesTriggers()
        {
            var keyword = new ActionKeyword { Id = 1, CommandName = "testkeyword" };
            _context.ActionKeywords.Add(keyword);
            _context.Triggers.Add(new TriggerType
            {
                Id = 1,
                Type = TriggerTypes.Keyword,
                Name = "oldkeyword",
                KeywordId = 1,
                Configuration = "{\"KeywordId\": 1, \"KeywordName\": \"oldkeyword\"}"
            });
            await _context.SaveChangesAsync();

            await _repository.UpdateKeywordTriggerConfigurationsForRenamedKeyword(1, "oldkeyword", "newkeyword");

            var saved = await _context.Triggers.FirstAsync();
            Assert.Contains("newkeyword", saved.Configuration);
        }

        #endregion

        #region UpdateTimerGroupNamesForRenamedTimerGroup

        [Fact]
        public async Task UpdateTimerGroupNamesForRenamedTimerGroup_UpdatesNames()
        {
            var action = new ActionType
            {
                Id = 1,
                Name = "Action1",
                SubActions = [
                    new TimerGroupSetEnabledStateType { Id = 1, TimerGroupId = 1, TimerGroupName = "OldName" }
                ]
            };
            _context.Actions.Add(action);
            await _context.SaveChangesAsync();

            await _repository.UpdateTimerGroupNamesForRenamedTimerGroup(1, "NewName");

            var saved = await _context.SubActions.OfType<TimerGroupSetEnabledStateType>().FirstAsync();
            Assert.Equal("NewName", saved.TimerGroupName);
        }

        #endregion

        #region RemapEntityReferencesAfterRestore

        [Fact]
        public async Task RemapEntityReferencesAfterRestore_RemapsTimerTriggers()
        {
            var timerGroup = new TimerGroup { Id = 1, Name = "TestTimer" };
            _context.TimerGroups.Add(timerGroup);
            
            var action = new ActionType
            {
                Id = 1,
                Name = "Action1",
                Triggers = [
                    new TriggerType
                    {
                        Id = 1,
                        Type = TriggerTypes.Timer,
                        Name = "OldTimer",
                        Configuration = "{\"TimerGroupId\": 999, \"TimerGroupName\": \"TestTimer\"}"
                    }
                ]
            };
            _context.Actions.Add(action);
            await _context.SaveChangesAsync();

            await _repository.RemapEntityReferencesAfterRestore(_logger);

            var saved = await _context.Triggers.FirstAsync();
            Assert.Equal(1, saved.TimerGroupId);
        }

        [Fact]
        public async Task RemapEntityReferencesAfterRestore_RemapsTimerTriggers_OldFormat()
        {
            var timerGroup = new TimerGroup { Id = 1, Name = "TestTimer" };
            _context.TimerGroups.Add(timerGroup);
            
            var action = new ActionType
            {
                Id = 1,
                Name = "Action1",
                Triggers = [
                    new TriggerType
                    {
                        Id = 1,
                        Type = TriggerTypes.Timer,
                        Name = "OldTimer",
                        Configuration = "{\"TimerGroupId\": 999}"
                    }
                ]
            };
            _context.Actions.Add(action);
            await _context.SaveChangesAsync();

            await _repository.RemapEntityReferencesAfterRestore(_logger);

            var saved = await _context.Triggers.FirstAsync();
            Assert.Null(saved.TimerGroupId);
        }

        #endregion
    }
}