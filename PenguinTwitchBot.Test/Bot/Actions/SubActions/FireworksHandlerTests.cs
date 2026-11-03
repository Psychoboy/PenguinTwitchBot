using System.Collections.Concurrent;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.WebSocketEvents;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class FireworksHandlerTests
    {
        [Fact]
        public async Task ExecuteAsync_ResolvesVariablesAndPublishesFireworksEvent()
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var handler = new FireworksHandler(webSocketMessenger);
            var subAction = new FireworksType
            {
                Count = "$math(%Amount% + 1)$",
                DelayMilliseconds = "%Delay%",
                Shell = "%Shell%",
                Size = "8"
            };
            var variables = new ConcurrentDictionary<string, string>
            {
                ["Amount"] = "2",
                ["Delay"] = "75",
                ["Shell"] = "Crossette"
            };

            await handler.ExecuteAsync(subAction, variables);

            await webSocketMessenger.Received(1).AddToQueue(Arg.Is<WsEvent>(message =>
                message.Event.Source == "SubAction" &&
                message.Event.Type == "Fireworks" &&
                (int)message.Data["count"] == 3 &&
                (int)message.Data["delayMs"] == 75 &&
                (string)message.Data["shell"] == "Crossette" &&
                (string)message.Data["size"] == "8"));
        }

        [Fact]
        public async Task ExecuteAsync_RejectsOutOfRangeResolvedCount()
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var handler = new FireworksHandler(webSocketMessenger);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => handler.ExecuteAsync(
                new FireworksType { Count = "%Count%" },
                new ConcurrentDictionary<string, string> { ["Count"] = "101" }));

            await webSocketMessenger.DidNotReceive().AddToQueue(Arg.Any<WsEvent>());
        }
    }
}