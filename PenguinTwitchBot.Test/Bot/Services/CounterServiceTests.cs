using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Moderation;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Bot.Services;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Test.Bot.Services
{
    public class CounterServiceTests
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICountersRepository _countersRepo;
        private readonly IActionCommandsRepository _actionCommandsRepo;
        private readonly IActionsRepository _actionsRepo;
        private readonly IHubContext<MainHub> _hubContext;
        private readonly ILogger<CounterService> _logger;
        private readonly CounterService _service;
        private readonly List<Counter> _dbCounters = [];
        private readonly List<ActionCommand> _dbCommands = [];
        private readonly List<ActionType> _dbActions = [];

        public CounterServiceTests()
        {
            _unitOfWork = Substitute.For<IUnitOfWork>();
            _countersRepo = Substitute.For<ICountersRepository>();
            _actionCommandsRepo = Substitute.For<IActionCommandsRepository>();
            _actionsRepo = Substitute.For<IActionsRepository>();

            _unitOfWork.Counters.Returns(_countersRepo);
            _unitOfWork.ActionCommands.Returns(_actionCommandsRepo);
            _unitOfWork.Actions.Returns(_actionsRepo);

            var transaction = Substitute.For<IDbContextTransaction>();
            transaction.CommitAsync().Returns(Task.CompletedTask);
            transaction.RollbackAsync().Returns(Task.CompletedTask);
            _unitOfWork.BeginTransactionAsync().Returns(transaction);

            _countersRepo.Find(Arg.Any<Expression<Func<Counter, bool>>>())
                .Returns(callInfo =>
                {
                    var predicate = callInfo.Arg<Expression<Func<Counter, bool>>>().Compile();
                    return _dbCounters.Where(predicate).ToList().BuildMockDbSet().AsQueryable();
                });
            _countersRepo.GetAsync().Returns(callInfo => Task.FromResult(_dbCounters.ToList()));
            _countersRepo.When(x => x.AddAsync(Arg.Any<Counter>()))
                .Do(callInfo =>
                {
                    var c = callInfo.Arg<Counter>();
                    _dbCounters.Add(c);
                });

            _actionCommandsRepo.Find(Arg.Any<Expression<Func<ActionCommand, bool>>>())
                .Returns(callInfo =>
                {
                    var predicate = callInfo.Arg<Expression<Func<ActionCommand, bool>>>().Compile();
                    return _dbCommands.Where(predicate).ToList().BuildMockDbSet().AsQueryable();
                });
            _actionCommandsRepo.When(x => x.AddAsync(Arg.Any<ActionCommand>()))
                .Do(callInfo =>
                {
                    var cmd = callInfo.Arg<ActionCommand>();
                    cmd.Id = 888;
                    _dbCommands.Add(cmd);
                });

            _actionsRepo.Find(Arg.Any<Expression<Func<ActionType, bool>>>())
                .Returns(callInfo =>
                {
                    var predicate = callInfo.Arg<Expression<Func<ActionType, bool>>>().Compile();
                    return _dbActions.Where(predicate).ToList().BuildMockDbSet().AsQueryable();
                });
            _actionsRepo.CreateActionAsync(Arg.Any<ActionType>())
                .Returns(callInfo =>
                {
                    var action = callInfo.Arg<ActionType>();
                    action.Id = 999;
                    _dbActions.Add(action);
                    return Task.FromResult(action);
                });

            var services = new ServiceCollection();
            services.AddSingleton(_unitOfWork);
            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            _hubContext = Substitute.For<IHubContext<MainHub>>();
            var clients = Substitute.For<IHubClients>();
            var clientProxy = Substitute.For<IClientProxy>();
            _hubContext.Clients.Returns(clients);
            clients.All.Returns(clientProxy);

            _logger = Substitute.For<ILogger<CounterService>>();

            _service = new CounterService(scopeFactory, _hubContext, _logger);
        }

        [Fact]
        public void PopulateVariables_SetsStandardAndDestinationVariables()
        {
            var dict = new ConcurrentDictionary<string, string>();
            var result = new CounterResult
            {
                Success = true,
                CounterName = "deaths",
                DisplayName = "Death Counter",
                OldValue = 3,
                NewValue = 4,
                Operation = CounterOperation.Increment
            };

            _service.PopulateVariables(dict, result, "my_custom_var");

            Assert.Equal("4", dict["counter_deaths"]);
            Assert.Equal("4", dict["counter_value"]);
            Assert.Equal("3", dict["counter_old_value"]);
            Assert.Equal("deaths", dict["counter_name"]);
            Assert.Equal("Death Counter", dict["counter_display_name"]);
            Assert.Equal("increment", dict["counter_operation"]);
            Assert.Equal("true", dict["counter_success"]);
            Assert.Equal("4", dict["my_custom_var"]);
        }

        [Fact]
        public async Task AdjustCounterAsync_Increment_RespectsStepAndMax()
        {
            var counter = new Counter
            {
                Id = 1,
                CounterName = "wins",
                Amount = 5,
                Step = 3,
                Max = 7,
                IncrementRank = Rank.Viewer
            };
            _dbCounters.Add(counter);

            var result = await _service.AdjustCounterAsync("wins", CounterOperation.Increment);

            Assert.True(result.Success);
            Assert.Equal(5, result.OldValue);
            Assert.Equal(7, result.NewValue); // Clamped at 7 rather than 8
            Assert.Equal(7, counter.Amount);
        }

        [Fact]
        public async Task AdjustCounterAsync_Decrement_RespectsMin()
        {
            var counter = new Counter
            {
                Id = 1,
                CounterName = "coins",
                Amount = 2,
                Step = 5,
                Min = 0,
                DecrementRank = Rank.Viewer
            };
            _dbCounters.Add(counter);

            var result = await _service.AdjustCounterAsync("coins", CounterOperation.Decrement);

            Assert.True(result.Success);
            Assert.Equal(2, result.OldValue);
            Assert.Equal(0, result.NewValue); // Clamped at 0
            Assert.Equal(0, counter.Amount);
        }

        [Fact]
        public async Task AdjustCounterAsync_Reset_ResetsToInitialValue()
        {
            var counter = new Counter
            {
                Id = 1,
                CounterName = "streak",
                Amount = 15,
                InitialValue = 0,
                ResetRank = Rank.Viewer
            };
            _dbCounters.Add(counter);

            var result = await _service.AdjustCounterAsync("streak", CounterOperation.Reset);

            Assert.True(result.Success);
            Assert.Equal(15, result.OldValue);
            Assert.Equal(0, result.NewValue);
            Assert.Equal(0, counter.Amount);
        }

        [Fact]
        public async Task AdjustCounterAsync_PermissionDenied_WhenRankTooLow()
        {
            var counter = new Counter
            {
                Id = 1,
                CounterName = "modonly",
                Amount = 0,
                IncrementRank = Rank.Moderator
            };
            _dbCounters.Add(counter);

            var callerArgs = new CommandEventArgs
            {
                Name = "viewer123",
                IsBroadcaster = false,
                IsMod = false,
                IsSub = false,
                IsVip = false
            };

            var result = await _service.AdjustCounterAsync("modonly", CounterOperation.Increment, callerArgs: callerArgs);

            Assert.False(result.Success);
            Assert.Contains("Insufficient permission", result.ErrorMessage);
            Assert.Equal(0, counter.Amount);
        }

        [Fact]
        public async Task AdjustCounterAsync_PermissionAllowed_WhenRankIsBroadcaster()
        {
            var counter = new Counter
            {
                Id = 1,
                CounterName = "admin_counter",
                Amount = 10,
                ResetRank = Rank.Streamer
            };
            _dbCounters.Add(counter);

            var callerArgs = new CommandEventArgs
            {
                Name = "streamer",
                IsBroadcaster = true
            };

            var result = await _service.AdjustCounterAsync("admin_counter", CounterOperation.Reset, callerArgs: callerArgs);

            Assert.True(result.Success);
            Assert.Equal(0, result.NewValue);
        }

        [Fact]
        public async Task EvaluateCommandArgsAsync_ParsesOperations()
        {
            var counter = new Counter
            {
                Id = 1,
                CounterName = "score",
                Amount = 10,
                Step = 2,
                IncrementRank = Rank.Viewer,
                DecrementRank = Rank.Viewer,
                ResetRank = Rank.Viewer,
                SetRank = Rank.Viewer
            };
            _dbCounters.Add(counter);

            // Test "+"
            var argsPlus = new CommandEventArgs { Arg = "+", Args = ["+"], IsBroadcaster = true };
            var res1 = await _service.EvaluateCommandArgsAsync("score", argsPlus);
            Assert.True(res1.Success);
            Assert.Equal(12, res1.NewValue);

            // Test "-"
            var argsMinus = new CommandEventArgs { Arg = "-", Args = ["-"], IsBroadcaster = true };
            var res2 = await _service.EvaluateCommandArgsAsync("score", argsMinus);
            Assert.True(res2.Success);
            Assert.Equal(10, res2.NewValue);

            // Test "set 50"
            var argsSet = new CommandEventArgs { Arg = "set 50", Args = ["set", "50"], IsBroadcaster = true };
            var res3 = await _service.EvaluateCommandArgsAsync("score", argsSet);
            Assert.True(res3.Success);
            Assert.Equal(50, res3.NewValue);

            // Test "reset"
            var argsReset = new CommandEventArgs { Arg = "reset", Args = ["reset"], IsBroadcaster = true };
            var res4 = await _service.EvaluateCommandArgsAsync("score", argsReset);
            Assert.True(res4.Success);
            Assert.Equal(0, res4.NewValue);

            // Test relative "+5"
            var argsPlusVal = new CommandEventArgs { Arg = "+5", Args = ["+5"], IsBroadcaster = true };
            var resPlusVal = await _service.EvaluateCommandArgsAsync("score", argsPlusVal);
            Assert.True(resPlusVal.Success);
            Assert.Equal(CounterOperation.Increment, resPlusVal.Operation);
            Assert.Equal(5, resPlusVal.NewValue);

            // Test relative "-2"
            var argsMinusVal = new CommandEventArgs { Arg = "-2", Args = ["-2"], IsBroadcaster = true };
            var resMinusVal = await _service.EvaluateCommandArgsAsync("score", argsMinusVal);
            Assert.True(resMinusVal.Success);
            Assert.Equal(CounterOperation.Decrement, resMinusVal.Operation);
            Assert.Equal(3, resMinusVal.NewValue);

            // Test unsigned numeric "42" (Set behavior)
            var argsUnsigned = new CommandEventArgs { Arg = "42", Args = ["42"], IsBroadcaster = true };
            var resUnsigned = await _service.EvaluateCommandArgsAsync("score", argsUnsigned);
            Assert.True(resUnsigned.Success);
            Assert.Equal(CounterOperation.Set, resUnsigned.Operation);
            Assert.Equal(42, resUnsigned.NewValue);

            // Test no args / empty (should GET value without modifying)
            var argsEmpty = new CommandEventArgs { Arg = "", Args = [], IsBroadcaster = false };
            var resEmpty = await _service.EvaluateCommandArgsAsync("score", argsEmpty);
            Assert.True(resEmpty.Success);
            Assert.Equal(CounterOperation.Get, resEmpty.Operation);
            Assert.Equal(42, resEmpty.NewValue);
            Assert.Equal(42, counter.Amount);
        }

        [Fact]
        public async Task AdjustCounterAsync_ConcurrentAdjustments_SerializeOperations()
        {
            var counter = new Counter
            {
                Id = 1,
                CounterName = "concurrent_counter",
                Amount = 0,
                Step = 1,
                IncrementRank = Rank.Viewer
            };
            _dbCounters.Add(counter);

            var tasks = Enumerable.Range(0, 20)
                .Select(_ => _service.AdjustCounterAsync("concurrent_counter", CounterOperation.Increment))
                .ToList();

            var results = await Task.WhenAll(tasks);

            Assert.All(results, r => Assert.True(r.Success));
            Assert.Equal(20, counter.Amount);
        }

        [Fact]
        public async Task UpdateCounterAsync_UpdatesExistingCounter()
        {
            var counter = new Counter
            {
                Id = 1,
                CounterName = "points",
                DisplayName = "Old Points",
                Amount = 10,
                Step = 1
            };
            _dbCounters.Add(counter);

            var updated = new Counter
            {
                Id = 1,
                CounterName = "points",
                DisplayName = "New Points",
                Amount = 25,
                Step = 5
            };

            var result = await _service.UpdateCounterAsync(updated);

            Assert.Equal("New Points", result.DisplayName);
            Assert.Equal(25, result.Amount);
            Assert.Equal(5, result.Step);
            _countersRepo.Received(1).Update(Arg.Any<Counter>());
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task CreateCounterWithActionAsync_WithOptions_CreatesCounterCommandActionAndSubActions()
        {
            var counter = new Counter
            {
                CounterName = "deaths",
                DisplayName = "Total Deaths",
                Amount = 0,
                Min = 0,
                Max = 1000
            };

            var options = new CounterActionCreationOptions
            {
                CreateActionAndCommand = true,
                CommandName = "deaths",
                ResponseMessage = "%counter_display_name%: %counter_value%",
                MinimumRank = Rank.Viewer,
                UserCooldown = 5,
                GlobalCooldown = 1,
                Group = "Counters"
            };

            var result = await _service.CreateCounterWithActionAsync(counter, options);

            Assert.Equal("deaths", result.CounterName);
            Assert.Contains(_dbCounters, c => c.CounterName == "deaths");

            // Verify ActionCommand created
            Assert.Single(_dbCommands);
            var cmd = _dbCommands[0];
            Assert.Equal("deaths", cmd.CommandName);
            Assert.Equal("Counters", cmd.Category);
            Assert.Equal(5, cmd.UserCooldown);
            Assert.Equal(1, cmd.GlobalCooldown);
            Assert.True(cmd.SourceOnly);
            Assert.Equal(Rank.Viewer, cmd.MinimumRank);

            // Verify Action created with SubActions and Triggers
            await _actionsRepo.Received(1).CreateActionAsync(Arg.Is<ActionType>(a =>
                a.Name == "deaths" &&
                a.Group == "Counters" &&
                a.SubActions.Count == 2 &&
                a.SubActions[0] is MultiCounterType &&
                ((MultiCounterType)a.SubActions[0]).Name == "deaths" &&
                ((MultiCounterType)a.SubActions[0]).Operation == CounterOperation.CommandArgs &&
                a.SubActions[1] is SendMessageType &&
                ((SendMessageType)a.SubActions[1]).Text == "%counter_display_name%: %counter_value%" &&
                a.Triggers.Count == 1 &&
                a.Triggers[0].Type == TriggerTypes.Command &&
                a.Triggers[0].Name == "!deaths"
            ));
        }

        [Fact]
        public async Task CreateCounterWithActionAsync_DisabledOption_CreatesCounterOnly()
        {
            var counter = new Counter
            {
                CounterName = "stars",
                DisplayName = "Stars"
            };

            var options = new CounterActionCreationOptions
            {
                CreateActionAndCommand = false
            };

            var result = await _service.CreateCounterWithActionAsync(counter, options);

            Assert.Equal("stars", result.CounterName);
            Assert.Contains(_dbCounters, c => c.CounterName == "stars");
            Assert.Empty(_dbCommands);
            await _actionsRepo.DidNotReceive().CreateActionAsync(Arg.Any<ActionType>());
        }

        [Fact]
        public async Task CreateCounterWithActionAsync_ExistingCommandName_ThrowsInvalidOperationException()
        {
            _dbCommands.Add(new ActionCommand { CommandName = "coins" });

            var counter = new Counter
            {
                CounterName = "coins"
            };

            var options = new CounterActionCreationOptions
            {
                CreateActionAndCommand = true,
                CommandName = "coins"
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateCounterWithActionAsync(counter, options));
        }

        [Fact]
        public async Task CreateCounterWithActionAsync_ExistingCounterName_ThrowsInvalidOperationException()
        {
            _dbCounters.Add(new Counter { CounterName = "kills" });

            var counter = new Counter
            {
                CounterName = "kills"
            };

            var options = new CounterActionCreationOptions
            {
                CreateActionAndCommand = true,
                CommandName = "kills"
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateCounterWithActionAsync(counter, options));
        }

        [Fact]
        public async Task CreateCounterWithActionAsync_WithoutResponseMessage_DoesNotAddSendMessageSubAction()
        {
            var counter = new Counter
            {
                CounterName = "silentcounter"
            };

            var options = new CounterActionCreationOptions
            {
                CreateActionAndCommand = true,
                CommandName = "silentcounter",
                ResponseMessage = null
            };

            await _service.CreateCounterWithActionAsync(counter, options);

            await _actionsRepo.Received(1).CreateActionAsync(Arg.Is<ActionType>(a =>
                a.SubActions.Count == 1 &&
                a.SubActions[0] is MultiCounterType
            ));
        }

        [Fact]
        public async Task CreateActionForCounterAsync_GeneratesCommandAndActionForExistingCounter()
        {
            var counter = new Counter
            {
                Id = 42,
                CounterName = "existingcounter",
                DisplayName = "Existing Counter"
            };

            var options = new CounterActionCreationOptions
            {
                CreateActionAndCommand = true,
                CommandName = "existingcounter",
                ResponseMessage = "%counter_display_name%: %counter_value%"
            };

            await _service.CreateActionForCounterAsync(counter, options);

            Assert.Contains(_dbCommands, c => c.CommandName == "existingcounter");
            await _actionsRepo.Received(1).CreateActionAsync(Arg.Is<ActionType>(a =>
                a.Name == "existingcounter" &&
                a.SubActions.Count == 2 &&
                a.Triggers.Count == 1 &&
                a.Triggers[0].Name == "!existingcounter"
            ));
        }
    }
}

