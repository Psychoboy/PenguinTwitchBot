using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Application.ChatMessage.Notification;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class ReplyToMessageHandlerTests
    {
        [Fact]
        public async Task ValidType_WithOriginalEventArgs_RepliesToMessage()
        {
            var chatBot = Substitute.For<ITwitchChatBot>();
            var dispatcher = Substitute.For<IPenguinDispatcher>();
            var twitchService = Substitute.For<ITwitchService>();
            var handler = new ReplyToMessageHandler(chatBot, dispatcher, twitchService);

            var eventArgs = new ChatMessageEventArgs { Name = "testuser", MessageId = "msg123", Message = "hello" };
            var variables = new ConcurrentDictionary<string, string>
            {
                ["OriginalEventArgs"] = JsonSerializer.Serialize(eventArgs)
            };

            var type = new ReplyToMessageType { Text = "hi there", StreamOnly = false };
            await handler.ExecuteAsync(type, variables);

            await chatBot.Received(1).ReplyToMessage("testuser", "msg123", "hi there", false);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var chatBot = Substitute.For<ITwitchChatBot>();
            var dispatcher = Substitute.For<IPenguinDispatcher>();
            var twitchService = Substitute.For<ITwitchService>();
            var handler = new ReplyToMessageHandler(chatBot, dispatcher, twitchService);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task UseBot_FallsBackToBotMessage()
        {
            var chatBot = Substitute.For<ITwitchChatBot>();
            var dispatcher = Substitute.For<IPenguinDispatcher>();
            var twitchService = Substitute.For<ITwitchService>();
            var handler = new ReplyToMessageHandler(chatBot, dispatcher, twitchService);

            var type = new ReplyToMessageType { Text = "bot reply", UseBot = true, StreamOnly = false };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            await dispatcher.Received(1).Publish(Arg.Is<SendBotMessage>(m => m.Message == "bot reply"));
        }
    }
}
