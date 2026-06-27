using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using NSubstitute;
using System.Collections.Concurrent;
using System.Text.Json;

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
    }
}
