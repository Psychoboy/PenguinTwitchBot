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

        [Fact]
        public void GetUIFields_ReturnsExpectedFields_WithoutExternalApiResponseHint()
        {
            var type = new TtsType();
            var fields = type.GetUIFields();

            Assert.DoesNotContain(fields, f => f.PropertyName == "info_hint");
            Assert.DoesNotContain(fields, f => f.Label != null && f.Label.Contains("ExternalApiResponse"));
            Assert.DoesNotContain(fields, f => f.HelperText != null && f.HelperText.Contains("ExternalApiResponse"));

            var textField = fields.FirstOrDefault(f => f.PropertyName == nameof(TtsType.Text));
            Assert.NotNull(textField);
            Assert.Equal("Message", textField.Label);
            Assert.True(textField.Resizable);
            Assert.Contains("%user%", textField.HelperText);
            Assert.Contains("%message%", textField.HelperText);
        }

        [Fact]
        public async Task ValidType_WhenRejectedByModeratorFilter_DoesNotSpeakMessage()
        {
            var ttsService = Substitute.For<ITTSService>();
            var moderatorFilter = Substitute.For<PenguinTwitchBot.Bot.Commands.Moderation.IModeratorFilterService>();
            moderatorFilter.IsPermittedAsync("bad message").Returns(false);

            var handler = new TtsHandler(ttsService, moderatorFilter);

            var type = new TtsType { Text = "bad message" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            await ttsService.DidNotReceive().SayMessage(Arg.Any<BaseVoice?>(), Arg.Any<string>());
        }

        [Fact]
        public async Task ValidType_WhenPermittedByModeratorFilter_SpeaksMessage()
        {
            var ttsService = Substitute.For<ITTSService>();
            var moderatorFilter = Substitute.For<PenguinTwitchBot.Bot.Commands.Moderation.IModeratorFilterService>();
            moderatorFilter.IsPermittedAsync("clean message").Returns(true);

            var voice = new RegisteredVoice { Id = 1, Name = "Test Voice" };
            ttsService.GetRandomVoice().Returns(voice);

            var handler = new TtsHandler(ttsService, moderatorFilter);

            var type = new TtsType { Text = "clean message" };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            await ttsService.Received(1).SayMessage(voice, "clean message");
        }
    }
}
