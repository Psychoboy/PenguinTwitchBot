using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class FishingHandlerTests
    {
        [Fact]
        public async Task FishingDisabled_ReturnsEarly()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var handler = new FishingHandler(logger, fishingService, gameplayService, webSocket);
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
            var handler = new FishingHandler(logger, fishingService, gameplayService, webSocket);

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
            var handler = new FishingHandler(logger, fishingService, gameplayService, webSocket);

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
            var handler = new FishingHandler(logger, fishingService, gameplayService, webSocket);

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

            var handler = new FishingHandler(logger, fishingService, gameplayService, webSocket);
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

            await webSocket.Received(1).AddToQueue(Arg.Any<string>());
        }

        [Fact]
        public async Task RodSnap_SendsRodSnappedMessage()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var handler = new FishingHandler(logger, fishingService, gameplayService, webSocket);
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

            await webSocket.Received(1).AddToQueue(Arg.Any<string>());
        }

        [Fact]
        public async Task SuccessfulCatch_SendsCatchMessage()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var handler = new FishingHandler(logger, fishingService, gameplayService, webSocket);
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

            await webSocket.Received(1).AddToQueue(Arg.Any<string>());
        }

        [Fact]
        public async Task GameplayException_ThrowsSubActionHandlerException()
        {
            var logger = Substitute.For<ILogger<FishingHandler>>();
            var fishingService = Substitute.For<IFishingService>();
            var gameplayService = Substitute.For<IFishingGameplayService>();
            var webSocket = Substitute.For<IWebSocketMessenger>();

            var handler = new FishingHandler(logger, fishingService, gameplayService, webSocket);
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

            var handler = new FishingHandler(logger, fishingService, gameplayService, webSocket);
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

            await webSocket.Received(3).AddToQueue(Arg.Any<string>());
        }

        [Fact]
        public void SupportedType_IsFishing()
        {
            var handler = new FishingHandler(
                Substitute.For<ILogger<FishingHandler>>(),
                Substitute.For<IFishingService>(),
                Substitute.For<IFishingGameplayService>(),
                Substitute.For<IWebSocketMessenger>());
            
            Assert.Equal(SubActionTypes.Fishing, handler.SupportedType);
        }
    }
}