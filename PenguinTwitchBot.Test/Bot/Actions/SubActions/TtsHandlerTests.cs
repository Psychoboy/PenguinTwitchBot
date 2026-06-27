using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.TTS;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class TtsHandlerTests
    {
        [Fact]
        public async Task ValidType_SpeaksMessage()
        {
            var ttsService = Substitute.For<ITTSService>();
            var handler = new TtsHandler(ttsService);

            var voice = new RegisteredVoice { Id = 1, Name = "Test Voice" };
            ttsService.GetRandomVoice().Returns(voice);

            var type = new TtsType { Text = "Hello World" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            await ttsService.Received(1).SayMessage(voice, "Hello World");
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var ttsService = Substitute.For<ITTSService>();
            var handler = new TtsHandler(ttsService);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task EmptyText_ThrowsException()
        {
            var ttsService = Substitute.For<ITTSService>();
            var handler = new TtsHandler(ttsService);

            var type = new TtsType { Text = "" };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
