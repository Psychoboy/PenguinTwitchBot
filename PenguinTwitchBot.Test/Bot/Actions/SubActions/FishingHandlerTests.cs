using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class FishingHandlerTests
    {
        private static FishingHandler CreateHandler(
            ILogger<FishingHandler> logger,
            IFishingService fishingService,
            IFishingGameplayService gameplayService,
            IWebSocketMessenger webSocket)
        {
            var services = new ServiceCollection();

            var actionManagementService = Substitute.For<IActionManagementService>();
            actionManagementService
                .GetActionsByTriggerTypeAndNameAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>())
                .Returns(_ => Task.FromResult(new List<ActionType>()));
            actionManagementService
                .GetActionsByTriggerTypeAndNameEnabledAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>())
                .Returns(_ => Task.FromResult(new List<ActionType>()));

            services.AddSingleton(actionManagementService);
            services.AddSingleton(Substitute.For<IAction>());

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            return new FishingHandler(logger, fishingService, gameplayService, webSocket, scopeFactory);
        }

        private static (FishingHandler Handler, IActionManagementService ActionManagementService, IAction ActionService) CreateHandlerWithActionMocks(
            ILogger<FishingHandler> logger,
            IFishingService fishingService,
            IFishingGameplayService gameplayService,
            IWebSocketMessenger webSocket)
        {
            var services = new ServiceCollection();

            var actionManagementService = Substitute.For<IActionManagementService>();
            actionManagementService
                .GetActionsByTriggerTypeAndNameAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>())
                .Returns(_ => Task.FromResult(new List<ActionType>()));
            actionManagementService
                .GetActionsByTriggerTypeAndNameEnabledAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>())
                .Returns(_ => Task.FromResult(new List<ActionType>()));

            var actionService = Substitute.For<IAction>();

            services.AddSingleton(actionManagementService);
            services.AddSingleton(actionService);

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            return (new FishingHandler(logger, fishingService, gameplayService, webSocket, scopeFactory), actionManagementService, actionService);
        }

        [Fact]
        public async Task FishingDisabled_ReturnsEarly()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var handler = CreateHandler(logger, fishingService, gameplayService, webSocket);
            fishingService.GetSettings().Returns(new FishingSettings { Enabled = false });

            var type = new FishingType();
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser", ["userid"] = "123" };

            await handler.ExecuteAsync(type, variables);

            await gameplayService.DidNotReceive().PerformFishingAttempt(Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();
            var handler = CreateHandler(logger, fishingService, gameplayService, webSocket);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task MissingUserVariable_ThrowsException()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();
            var handler = CreateHandler(logger, fishingService, gameplayService, webSocket);

            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true });

            var type = new FishingType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }

        [Fact]
        public async Task MissingUserIdVariable_ThrowsException()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();
            var handler = CreateHandler(logger, fishingService, gameplayService, webSocket);

            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true });

            var type = new FishingType();
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser" };

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }

        [Fact]
        public async Task LineSnap_SendsSnappedMessage()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var handler = CreateHandler(logger, fishingService, gameplayService, webSocket);
            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true, DisplayDurationMs = 100 });

            var type = new FishingType { Attempts = 1 };
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser", ["userid"] = "123" };

            gameplayService.PerformFishingAttempt("123", "testuser").Returns(
                new FishingAttemptResult
                {
                    Outcome = FishingAttemptOutcome.LineSnapped,
                    FishCatch = null
                });

            await handler.ExecuteAsync(type, variables);

            await webSocket.Received(1).AddToQueue(WsTopics.Fishing, Arg.Any<string>());
        }

        [Fact]
        public async Task RodSnap_SendsRodSnappedMessage()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var handler = CreateHandler(logger, fishingService, gameplayService, webSocket);
            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true, DisplayDurationMs = 100 });

            var type = new FishingType { Attempts = 1 };
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser", ["userid"] = "123" };

            gameplayService.PerformFishingAttempt("123", "testuser").Returns(
                new FishingAttemptResult
                {
                    Outcome = FishingAttemptOutcome.RodSnapped,
                    FishCatch = null
                });

            await handler.ExecuteAsync(type, variables);

            await webSocket.Received(1).AddToQueue(WsTopics.Fishing, Arg.Any<string>());
        }

        [Fact]
        public async Task SuccessfulCatch_SendsCatchMessage()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var handler = CreateHandler(logger, fishingService, gameplayService, webSocket);
            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true, DisplayDurationMs = 100 });

            var type = new FishingType { Attempts = 1 };
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser", ["userid"] = "123" };

            var fishCatch = new FishCatch
            {
                FishType = new FishType { Name = "Golden Carp", Rarity = FishRarity.Epic, ImageFileName = "golden_carp.png" },
                Stars = 3,
                Weight = 5.5,
                GoldEarned = 100
            };

            gameplayService.PerformFishingAttempt("123", "testuser").Returns(
                new FishingAttemptResult
                {
                    Outcome = FishingAttemptOutcome.CaughtFish,
                    FishCatch = fishCatch
                });

            await handler.ExecuteAsync(type, variables);

            await webSocket.Received(1).AddToQueue(WsTopics.Fishing, Arg.Any<string>());
        }

        [Fact]
        public async Task GameplayException_ThrowsSubActionHandlerException()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var handler = CreateHandler(logger, fishingService, gameplayService, webSocket);
            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true, DisplayDurationMs = 100 });

            var type = new FishingType { Attempts = 1 };
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser", ["userid"] = "123" };

            gameplayService.When(x => x.PerformFishingAttempt("123", "testuser"))
                .Do(_ => throw new InvalidOperationException("Database error"));

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }

        [Fact]
        public async Task MultipleAttempts_SendsMultipleMessages()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var handler = CreateHandler(logger, fishingService, gameplayService, webSocket);
            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true, DisplayDurationMs = 100 });

            var type = new FishingType { Attempts = 3 };
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser", ["userid"] = "123" };

            gameplayService.PerformFishingAttempt("123", "testuser").Returns(
                new FishingAttemptResult
                {
                    Outcome = FishingAttemptOutcome.CaughtFish,
                    FishCatch = new FishCatch
                    {
                        FishType = new FishType { Name = "Trout", Rarity = FishRarity.Common, ImageFileName = "trout.png" },
                        Stars = 1,
                        Weight = 1.0,
                        GoldEarned = 10
                    }
                });

            await handler.ExecuteAsync(type, variables);

            await webSocket.Received(3).AddToQueue(WsTopics.Fishing, Arg.Any<string>());
        }

        [Fact]
        public async Task ReelJam_SendsReelJammedMessage()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var handler = CreateHandler(logger, fishingService, gameplayService, webSocket);
            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true, DisplayDurationMs = 100 });

            var type = new FishingType { Attempts = 1 };
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser", ["userid"] = "123" };

            gameplayService.PerformFishingAttempt("123", "testuser").Returns(
                new FishingAttemptResult
                {
                    Outcome = FishingAttemptOutcome.ReelJammed,
                    FishCatch = null
                });

            await handler.ExecuteAsync(type, variables);

            await webSocket.Received(1).AddToQueue(WsTopics.Fishing, Arg.Is<string>(msg => msg.Contains("\"reelJammed\":true") && msg.Contains("\"accidentReason\":\"REEL JAMMED\"")));
            Assert.Equal("REEL JAMMED", variables["fish_type"]);
            Assert.Equal("Accident", variables["fish_rarity"]);
        }

        [Fact]
        public async Task TackleBoxLost_SendsTackleBoxLostMessage()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var handler = CreateHandler(logger, fishingService, gameplayService, webSocket);
            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true, DisplayDurationMs = 100 });

            var type = new FishingType { Attempts = 1 };
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser", ["userid"] = "123" };

            gameplayService.PerformFishingAttempt("123", "testuser").Returns(
                new FishingAttemptResult
                {
                    Outcome = FishingAttemptOutcome.TackleBoxLost,
                    FishCatch = null
                });

            await handler.ExecuteAsync(type, variables);

            await webSocket.Received(1).AddToQueue(WsTopics.Fishing, Arg.Is<string>(msg => msg.Contains("\"tackleBoxLost\":true") && msg.Contains("\"accidentReason\":\"TACKLE BOX LOST\"")));
            Assert.Equal("TACKLE BOX LOST", variables["fish_type"]);
            Assert.Equal("Accident", variables["fish_rarity"]);
        }

        [Fact]
        public async Task NetBreak_SendsNetBrokenMessage()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var handler = CreateHandler(logger, fishingService, gameplayService, webSocket);
            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true, DisplayDurationMs = 100 });

            var type = new FishingType { Attempts = 1 };
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser", ["userid"] = "123" };

            gameplayService.PerformFishingAttempt("123", "testuser").Returns(
                new FishingAttemptResult
                {
                    Outcome = FishingAttemptOutcome.NetBroken,
                    FishCatch = null
                });

            await handler.ExecuteAsync(type, variables);

            await webSocket.Received(1).AddToQueue(WsTopics.Fishing, Arg.Is<string>(msg => msg.Contains("\"netBroken\":true") && msg.Contains("\"accidentReason\":\"NET BROKEN\"")));
            Assert.Equal("NET BROKEN", variables["fish_type"]);
            Assert.Equal("Accident", variables["fish_rarity"]);
        }

        [Fact]
        public void SupportedType_IsFishing()
        {
            var handler = CreateHandler(
                Substitute.For<ILogger<FishingHandler>>(),
                Substitute.For<IFishingService>(),
                Substitute.For<IFishingGameplayService>(),
                Substitute.For<IWebSocketMessenger>());

            Assert.Equal(SubActionTypes.Fishing, handler.SupportedType);
        }

        [Fact]
        public async Task Accident_FiresAccidentTrigger_WithVariablesAndConfiguration()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var (handler, actionManagement, actionService) = CreateHandlerWithActionMocks(logger, fishingService, gameplayService, webSocket);
            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true, DisplayDurationMs = 100 });

            var config = new FishingAccidentTriggerConfiguration
            {
                AccidentTypes = new List<FishingAttemptOutcome> { FishingAttemptOutcome.RodSnapped }
            };

            var action = new ActionType
            {
                Name = "OnRodSnappedAction",
                Triggers = new List<TriggerType>
                {
                    new()
                    {
                        Name = FishingHandler.FishingAccidentTriggerName,
                        Type = TriggerTypes.FishingAccident,
                        Enabled = true,
                        Configuration = JsonSerializer.Serialize(config)
                    }
                }
            };

            actionManagement
                .GetActionsByTriggerTypeAndNameEnabledAsync(TriggerTypes.FishingAccident, FishingHandler.FishingAccidentTriggerName)
                .Returns(Task.FromResult(new List<ActionType> { action }));

            var type = new FishingType { Attempts = 1 };
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser", ["userid"] = "123" };

            var snapEvent = new FishingSnapEvent
            {
                SnapType = "Rod",
                TotalGoldLost = 250
            };

            gameplayService.PerformFishingAttempt("123", "testuser").Returns(
                new FishingAttemptResult
                {
                    Outcome = FishingAttemptOutcome.RodSnapped,
                    SnapEvent = snapEvent
                });

            await handler.ExecuteAsync(type, variables);

            await actionService.Received(1).EnqueueAction(
                Arg.Is<ConcurrentDictionary<string, string>>(v =>
                    v["accident_type"] == "RodSnapped" &&
                    v["accident_reason"] == "ROD SNAPPED" &&
                    v["accident_gear"] == "Rod" &&
                    v["accident_gold_lost"] == "250"),
                action);
        }

        [Fact]
        public async Task Accident_DoesNotFireAccidentTrigger_WhenOutcomeDoesNotMatchConfiguration()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var (handler, actionManagement, actionService) = CreateHandlerWithActionMocks(logger, fishingService, gameplayService, webSocket);
            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true, DisplayDurationMs = 100 });

            var config = new FishingAccidentTriggerConfiguration
            {
                AccidentTypes = new List<FishingAttemptOutcome> { FishingAttemptOutcome.NetBroken }
            };

            var action = new ActionType
            {
                Name = "OnNetBrokenAction",
                Triggers = new List<TriggerType>
                {
                    new()
                    {
                        Name = FishingHandler.FishingAccidentTriggerName,
                        Type = TriggerTypes.FishingAccident,
                        Enabled = true,
                        Configuration = JsonSerializer.Serialize(config)
                    }
                }
            };

            actionManagement
                .GetActionsByTriggerTypeAndNameEnabledAsync(TriggerTypes.FishingAccident, FishingHandler.FishingAccidentTriggerName)
                .Returns(Task.FromResult(new List<ActionType> { action }));

            var type = new FishingType { Attempts = 1 };
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser", ["userid"] = "123" };

            gameplayService.PerformFishingAttempt("123", "testuser").Returns(
                new FishingAttemptResult
                {
                    Outcome = FishingAttemptOutcome.RodSnapped,
                    SnapEvent = new FishingSnapEvent { SnapType = "Rod" }
                });

            await handler.ExecuteAsync(type, variables);

            await actionService.DidNotReceive().EnqueueAction(Arg.Any<ConcurrentDictionary<string, string>>(), Arg.Any<ActionType>());
        }

        [Fact]
        public async Task DurabilityBroken_FiresItemBrokenTrigger_WithVariablesAndConfiguration()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var (handler, actionManagement, actionService) = CreateHandlerWithActionMocks(logger, fishingService, gameplayService, webSocket);
            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true, DisplayDurationMs = 100 });

            var config = new FishingItemBrokenTriggerConfiguration
            {
                EquipmentSlots = new List<string> { nameof(EquipmentSlot.Rod) }
            };

            var action = new ActionType
            {
                Name = "OnRodBrokenAction",
                Triggers = new List<TriggerType>
                {
                    new()
                    {
                        Name = FishingHandler.FishingItemBrokenTriggerName,
                        Type = TriggerTypes.FishingItemBroken,
                        Enabled = true,
                        Configuration = JsonSerializer.Serialize(config)
                    }
                }
            };

            actionManagement
                .GetActionsByTriggerTypeAndNameEnabledAsync(TriggerTypes.FishingItemBroken, FishingHandler.FishingItemBrokenTriggerName)
                .Returns(Task.FromResult(new List<ActionType> { action }));

            var type = new FishingType { Attempts = 1 };
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser", ["userid"] = "123" };

            var brokenItem = new FishingBrokenItemInfo
            {
                UserBoostId = 5,
                ShopItemId = 12,
                ItemName = "Fiberglass Rod",
                EquipmentSlot = EquipmentSlot.Rod,
                ItemCost = 200,
                WasReplaced = false
            };

            gameplayService.PerformFishingAttempt("123", "testuser").Returns(
                new FishingAttemptResult
                {
                    Outcome = FishingAttemptOutcome.CaughtFish,
                    FishCatch = new FishCatch
                    {
                        FishType = new FishType { Name = "Bass", Rarity = FishRarity.Common },
                        Weight = 2.0,
                        GoldEarned = 10
                    },
                    BrokenItems = new List<FishingBrokenItemInfo> { brokenItem }
                });

            await handler.ExecuteAsync(type, variables);

            await actionService.Received(1).EnqueueAction(
                Arg.Is<ConcurrentDictionary<string, string>>(v =>
                    v["broken_item_name"] == "Fiberglass Rod" &&
                    v["broken_item_slot"] == "Rod" &&
                    v["broken_item_cost"] == "200" &&
                    v["broken_item_replaced"] == "false" &&
                    v["broken_items_count"] == "1"),
                action);
        }

        [Fact]
        public async Task DurabilityBroken_DoesNotFireItemBrokenTrigger_WhenSlotDoesNotMatchConfiguration()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var (handler, actionManagement, actionService) = CreateHandlerWithActionMocks(logger, fishingService, gameplayService, webSocket);
            fishingService.GetSettings().Returns(new FishingSettings { Enabled = true, DisplayDurationMs = 100 });

            var config = new FishingItemBrokenTriggerConfiguration
            {
                EquipmentSlots = new List<string> { nameof(EquipmentSlot.Net) }
            };

            var action = new ActionType
            {
                Name = "OnNetBrokenAction",
                Triggers = new List<TriggerType>
                {
                    new()
                    {
                        Name = FishingHandler.FishingItemBrokenTriggerName,
                        Type = TriggerTypes.FishingItemBroken,
                        Enabled = true,
                        Configuration = JsonSerializer.Serialize(config)
                    }
                }
            };

            actionManagement
                .GetActionsByTriggerTypeAndNameEnabledAsync(TriggerTypes.FishingItemBroken, FishingHandler.FishingItemBrokenTriggerName)
                .Returns(Task.FromResult(new List<ActionType> { action }));

            var type = new FishingType { Attempts = 1 };
            var variables = new ConcurrentDictionary<string, string> { ["user"] = "testuser", ["userid"] = "123" };

            var brokenItem = new FishingBrokenItemInfo
            {
                UserBoostId = 5,
                ShopItemId = 12,
                ItemName = "Fiberglass Rod",
                EquipmentSlot = EquipmentSlot.Rod,
                ItemCost = 200,
                WasReplaced = false
            };

            gameplayService.PerformFishingAttempt("123", "testuser").Returns(
                new FishingAttemptResult
                {
                    Outcome = FishingAttemptOutcome.CaughtFish,
                    FishCatch = new FishCatch
                    {
                        FishType = new FishType { Name = "Bass", Rarity = FishRarity.Common },
                        Weight = 2.0,
                        GoldEarned = 10
                    },
                    BrokenItems = new List<FishingBrokenItemInfo> { brokenItem }
                });

            await handler.ExecuteAsync(type, variables);

            await actionService.DidNotReceive().EnqueueAction(Arg.Any<ConcurrentDictionary<string, string>>(), Arg.Any<ActionType>());
        }
    }
}
