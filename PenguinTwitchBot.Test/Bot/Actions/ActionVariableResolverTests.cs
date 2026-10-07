using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.Variables;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Repository;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Actions
{
    public class ActionVariableResolverTests
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ActionVariableResolver> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ActionVariableResolver _resolver;

        public ActionVariableResolverTests()
        {
            _unitOfWork = Substitute.For<IUnitOfWork>();
            _logger = Substitute.For<ILogger<ActionVariableResolver>>();

            var globalVarRepo = Substitute.For<IGlobalVariablesRepository>();
            globalVarRepo.GetAllAsync().Returns(new List<GlobalVariable>
            {
                new() { Name = "StreamGoal", Value = "100" }
            });
            _unitOfWork.GlobalVariables.Returns(globalVarRepo);

            var actionsRepo = Substitute.For<IActionsRepository>();
            actionsRepo.GetAllWithDetailsAsync().Returns(new List<ActionType>());
            _unitOfWork.Actions.Returns(actionsRepo);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_unitOfWork);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            _scopeFactory = Substitute.For<IServiceScopeFactory>();
            #pragma warning disable NS1000
            _scopeFactory.CreateScope().Returns(_ => serviceProvider.CreateScope());
            #pragma warning restore NS1000

            _resolver = new ActionVariableResolver(_scopeFactory, _logger);
        }

        [Fact]
        public async Task ResolveVariablesAsync_IncludesSystemGlobals_And_UserGlobalsOnlyWhenGetGlobalVariableUsed()
        {
            // 1. Without GetGlobalVariable subaction: DB globals are NOT present
            var resultWithoutGetGlobal = await _resolver.ResolveVariablesAsync(
                currentActionId: null,
                triggers: null,
                previousSubActions: null,
                isCatchSubAction: false);

            Assert.DoesNotContain(resultWithoutGetGlobal, v => v.Name.Equals("StreamGoal", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(resultWithoutGetGlobal, v => v.Name.Equals("$math", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(resultWithoutGetGlobal, v => v.Name.Equals("bot", System.StringComparison.OrdinalIgnoreCase) && v.Category == "System");
            Assert.Contains(resultWithoutGetGlobal, v => v.Name.Equals("streamer", System.StringComparison.OrdinalIgnoreCase) && v.Category == "System");
            Assert.Contains(resultWithoutGetGlobal, v => v.Name.Equals("user", System.StringComparison.OrdinalIgnoreCase) && v.Category == "System");
            Assert.Contains(resultWithoutGetGlobal, v => v.Name.Equals("date", System.StringComparison.OrdinalIgnoreCase) && v.Category == "System");
            Assert.Contains(resultWithoutGetGlobal, v => v.Name.Equals("time", System.StringComparison.OrdinalIgnoreCase) && v.Category == "System");
            Assert.Contains(resultWithoutGetGlobal, v => v.Name.Equals("ticks", System.StringComparison.OrdinalIgnoreCase) && v.Category == "System");
            Assert.Contains(resultWithoutGetGlobal, v => v.Name.Equals("random", System.StringComparison.OrdinalIgnoreCase) && v.Category == "System");

            // 2. With GetGlobalVariable subaction: Global appears under Previous Steps
            var previousSteps = new List<SubActionType>
            {
                new GetGlobalVariableType { Text = "StreamGoal", TargetVariableName = "GoalLoaded" }
            };

            var resultWithGetGlobal = await _resolver.ResolveVariablesAsync(
                currentActionId: null,
                triggers: null,
                previousSubActions: previousSteps,
                isCatchSubAction: false);

            var goalVar = resultWithGetGlobal.FirstOrDefault(v => v.Name.Equals("GoalLoaded", System.StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(goalVar);
            Assert.Equal("Previous Steps", goalVar.Category);
            Assert.Equal("SavedValue", goalVar.ExampleValue);
            Assert.Contains("StreamGoal", goalVar.Description);
        }

        [Fact]
        public async Task ResolveVariablesAsync_IncludesTriggerVariables()
        {
            var triggers = new List<TriggerType>
            {
                new TriggerType { Type = TriggerTypes.Command, Name = "!test" }
            };

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: triggers,
                previousSubActions: null,
                isCatchSubAction: false);

            var userVar = result.FirstOrDefault(v => v.Name.Equals("User", System.StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(userVar);
            Assert.StartsWith("Trigger: Command (!test)", userVar.Source);

            Assert.Contains(result, v => v.Name.Equals("Name", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("Args", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("TargetUser", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveVariablesAsync_SubActionOverwritesTrigger_DeduplicatesAndShowsFinalSource()
        {
            var triggers = new List<TriggerType>
            {
                new TriggerType { Type = TriggerTypes.Command, Name = "!shoutout" }
            };

            var previousSubActions = new List<SubActionType>
            {
                new SetVariableType { Text = "TargetUser", Value = "Penguin" }
            };

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: triggers,
                previousSubActions: previousSubActions,
                isCatchSubAction: false);

            var targetUserMatches = result.Where(v => v.Name.Equals("TargetUser", System.StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.Single(targetUserMatches);
            Assert.Equal("Step 1", targetUserMatches[0].Source);
        }

        [Fact]
        public async Task ResolveVariablesAsync_InspectsLogicIfElseBranches()
        {
            var ifElseSubAction = new LogicIfElseType
            {
                TrueSubActions = new List<SubActionType>
                {
                    new SetVariableType { Text = "BranchResult", Value = "TrueBranch" }
                },
                FalseSubActions = new List<SubActionType>
                {
                    new SetVariableType { Text = "AlternativeResult", Value = "FalseBranch" }
                }
            };

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: null,
                previousSubActions: new List<SubActionType> { ifElseSubAction },
                isCatchSubAction: false);

            Assert.Contains(result, v => v.Name.Equals("BranchResult", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("AlternativeResult", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveVariablesAsync_CatchBlock_IncludesActionErrorMessage()
        {
            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: null,
                previousSubActions: null,
                isCatchSubAction: true);

            var errorVar = result.FirstOrDefault(v => v.Name.Equals("ActionErrorMessage", System.StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(errorVar);
            Assert.Equal("Catch Block", errorVar.Category);
        }

        [Fact]
        public async Task ResolveVariablesAsync_ResolvesCallerActions_WithoutInfiniteLoop()
        {
            var action1 = new ActionType
            {
                Id = 1,
                Name = "Parent Action",
                Triggers = new List<TriggerType> { new TriggerType { Type = TriggerTypes.Command, Name = "!parent" } },
                SubActions = new List<SubActionType>
                {
                    new SetVariableType { Text = "ParentVar", Value = "123" },
                    new ExecuteActionType { ActionId = 2, Text = "hello" }
                }
            };

            var action2 = new ActionType
            {
                Id = 2,
                Name = "Child Action",
                Triggers = new List<TriggerType>(),
                SubActions = new List<SubActionType>
                {
                    new ExecuteActionType { ActionId = 1 }
                }
            };

            _unitOfWork.Actions.GetAllWithDetailsAsync().Returns(new List<ActionType> { action1, action2 });

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 2,
                triggers: null,
                previousSubActions: null,
                isCatchSubAction: false);

            Assert.Contains(result, v => v.Name.Equals("ParentVar", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("Args", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void DependencyInjection_CanActivate_ActionVariableResolver_WithoutAmbiguity()
        {
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IUnitOfWork>());
            services.AddSingleton(Substitute.For<ILogger<ActionVariableResolver>>());
            services.AddSingleton(Substitute.For<IServiceScopeFactory>());
            services.AddScoped<IActionVariableResolver, ActionVariableResolver>();

            var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            using var scope = provider.CreateScope();
            var instance = scope.ServiceProvider.GetRequiredService<IActionVariableResolver>();
            Assert.NotNull(instance);
        }

        [Fact]
        public async Task ResolveVariablesAsync_DefaultCommand_SpinWheelResult_IncludesWheelVariables()
        {
            var triggers = new List<TriggerType>
            {
                new()
                {
                    Type = TriggerTypes.DefaultCommand,
                    Name = "spinwheel_result",
                    Configuration = "{\"DefaultCommandName\":\"spinwheel\",\"EventType\":\"WheelSpin.Result\"}"
                }
            };

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: triggers,
                previousSubActions: null,
                isCatchSubAction: false);

            Assert.Contains(result, v => v.Name.Equals("WheelSpinResult", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("WinningLabel", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("WinningMessage", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("WheelName", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("WinningIndex", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("IsNameWheel", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("User", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("Args", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveVariablesAsync_DefaultCommand_SpinWheelResult_ByNameWithoutConfig_ResolvesVariables()
        {
            var triggers = new List<TriggerType>
            {
                new()
                {
                    Type = TriggerTypes.DefaultCommand,
                    Name = "spinwheel_result"
                }
            };

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: triggers,
                previousSubActions: null,
                isCatchSubAction: false);

            Assert.Contains(result, v => v.Name.Equals("WheelSpinResult", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("WinningLabel", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("WinningMessage", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("WheelName", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("WinningIndex", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("IsNameWheel", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveVariablesAsync_DefaultCommand_GambleJackpot_IncludesGambleVariables()
        {
            var triggers = new List<TriggerType>
            {
                new()
                {
                    Type = TriggerTypes.DefaultCommand,
                    Name = "gamble_JackpotWin",
                    Configuration = "{\"DefaultCommandName\":\"gamble\",\"EventType\":\"Gamble.JackpotWin\"}"
                }
            };

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: triggers,
                previousSubActions: null,
                isCatchSubAction: false);

            Assert.Contains(result, v => v.Name.Equals("JackpotAmount", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("WinAmount", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("TotalWinnings", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("RolledValue", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveVariablesAsync_DefaultCommand_Defuse_IncludesDefuseVariables()
        {
            var triggers = new List<TriggerType>
            {
                new()
                {
                    Type = TriggerTypes.DefaultCommand,
                    Name = "defuse_Success",
                    Configuration = "{\"DefaultCommandName\":\"defuse\",\"EventType\":\"Defuse.Success\"}"
                }
            };

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: triggers,
                previousSubActions: null,
                isCatchSubAction: false);

            Assert.Contains(result, v => v.Name.Equals("ChosenWire", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("CorrectWire", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("WinAmount", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveVariablesAsync_TimerTrigger_IncludesTimerVariables()
        {
            var triggers = new List<TriggerType>
            {
                new()
                {
                    Type = TriggerTypes.Timer,
                    Name = "TimerGroup_Socials",
                    Configuration = "{\"TimerGroupId\":5,\"TimerGroupName\":\"Socials\"}"
                }
            };

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: triggers,
                previousSubActions: null,
                isCatchSubAction: false);

            Assert.Contains(result, v => v.Name.Equals("timer_name", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("timer_id", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("TimerInterval", System.StringComparison.OrdinalIgnoreCase));

            var timerName = result.First(v => v.Name.Equals("timer_name", System.StringComparison.OrdinalIgnoreCase));
            Assert.Equal("Socials", timerName.ExampleValue);
            var timerId = result.First(v => v.Name.Equals("timer_id", System.StringComparison.OrdinalIgnoreCase));
            Assert.Equal("5", timerId.ExampleValue);
        }

        [Fact]
        public async Task ResolveVariablesAsync_BannedSongRequest_IncludesBannedSongVariables()
        {
            var triggers = new List<TriggerType>
            {
                new()
                {
                    Type = TriggerTypes.BannedSongRequest,
                    Name = "Song.BannedRequest"
                }
            };

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: triggers,
                previousSubActions: null,
                isCatchSubAction: false);

            Assert.Contains(result, v => v.Name.Equals("banned_song_id", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("banned_song_title", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("banned_song_reason", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("banned_song_url", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveVariablesAsync_FishingItemBroken_IncludesBrokenItemVariables()
        {
            var triggers = new List<TriggerType>
            {
                new()
                {
                    Type = TriggerTypes.FishingItemBroken,
                    Name = "Fishing.ItemBroken"
                }
            };

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: triggers,
                previousSubActions: null,
                isCatchSubAction: false);

            Assert.Contains(result, v => v.Name.Equals("broken_item_name", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("broken_item_slot", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("broken_item_cost", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveVariablesAsync_FishingAccident_IncludesAccidentVariables()
        {
            var triggers = new List<TriggerType>
            {
                new()
                {
                    Type = TriggerTypes.FishingAccident,
                    Name = "Fishing.Accident"
                }
            };

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: triggers,
                previousSubActions: null,
                isCatchSubAction: false);

            Assert.Contains(result, v => v.Name.Equals("accident_type", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("accident_name", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("accident_gold_lost", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveVariablesAsync_ManualTrigger_IncludesUserVariables()
        {
            var triggers = new List<TriggerType>
            {
                new()
                {
                    Type = TriggerTypes.Manual,
                    Name = "Manual"
                }
            };

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: triggers,
                previousSubActions: null,
                isCatchSubAction: false);

            Assert.Contains(result, v => v.Name.Equals("User", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("DisplayName", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveVariablesAsync_DisabledSubAction_ExcludedFromVariables()
        {
            var previousSubActions = new List<SubActionType>
            {
                new SetVariableType { Text = "DisabledVar", Value = "Test", Enabled = false },
                new SetVariableType { Text = "EnabledVar", Value = "Test", Enabled = true }
            };

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 1,
                triggers: null,
                previousSubActions: previousSubActions,
                isCatchSubAction: false);

            Assert.DoesNotContain(result, v => v.Name.Equals("DisabledVar", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("EnabledVar", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveVariablesAsync_CallerInNestedIfElseOrCatch_ResolvesCallerVariables()
        {
            var callerAction = new ActionType
            {
                Id = 10,
                Name = "ParentAction",
                SubActions = new List<SubActionType>
                {
                    new SetVariableType { Text = "ParentPreVar", Value = "Pre" },
                    new LogicIfElseType
                    {
                        TrueSubActions = new List<SubActionType>
                        {
                            new ExecuteActionType { ActionId = 20 }
                        }
                    }
                }
            };

            _unitOfWork.Actions.GetAllWithDetailsAsync().Returns(new List<ActionType> { callerAction });

            var result = await _resolver.ResolveVariablesAsync(
                currentActionId: 20,
                triggers: null,
                previousSubActions: null,
                isCatchSubAction: false);

            Assert.Contains(result, v => v.Name.Equals("ParentPreVar", System.StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result, v => v.Name.Equals("Args", System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
