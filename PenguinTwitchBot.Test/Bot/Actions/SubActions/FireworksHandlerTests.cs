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
                StartDelayMilliseconds = "%StartDelay%",
                DelayMilliseconds = "%Delay%",
                DelayVarianceMilliseconds = "$math(%Variance% + 5)$",
                Shell = "%Shell%",
                Size = "5"
            };
            var variables = new ConcurrentDictionary<string, string>
            {
                ["Amount"] = "2",
                ["StartDelay"] = "900",
                ["Delay"] = "75",
                ["Variance"] = "20",
                ["Shell"] = "Crossette"
            };

            await handler.ExecuteAsync(subAction, variables);

            await webSocketMessenger.Received(1).AddToQueue(Arg.Is<WsEvent>(message =>
                message.Event.Source == "SubAction" &&
                message.Event.Type == "Fireworks" &&
                Guid.Parse((string)message.Data["jobId"]) != Guid.Empty &&
                (int)message.Data["count"] == 3 &&
                (int)message.Data["startDelayMs"] == 900 &&
                (int)message.Data["delayMs"] == 75 &&
                (int)message.Data["delayVarianceMs"] == 25 &&
                (string)message.Data["shell"] == "Crossette" &&
                (string)message.Data["size"] == "5"));
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

        [Theory]
        [InlineData("-0.1")]
        [InlineData("5.1")]
        [InlineData("NaN")]
        public async Task ExecuteAsync_RejectsOutOfRangeResolvedSize(string size)
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var handler = new FireworksHandler(webSocketMessenger);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => handler.ExecuteAsync(
                new FireworksType { Size = size },
                new ConcurrentDictionary<string, string>()));

            await webSocketMessenger.DidNotReceive().AddToQueue(Arg.Any<WsEvent>());
        }

        [Fact]
        public async Task ExecuteAsync_AllowsZeroSizeAndEmptyOverride()
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var handler = new FireworksHandler(webSocketMessenger);

            await handler.ExecuteAsync(new FireworksType { Size = "0" }, new ConcurrentDictionary<string, string>());
            await handler.ExecuteAsync(new FireworksType { Size = "" }, new ConcurrentDictionary<string, string>());

            await webSocketMessenger.Received(2).AddToQueue(Arg.Is<WsEvent>(message =>
                message.Data.ContainsKey("size") &&
                ((string)message.Data["size"] == "0" || (string)message.Data["size"] == "")));
        }
    }
}