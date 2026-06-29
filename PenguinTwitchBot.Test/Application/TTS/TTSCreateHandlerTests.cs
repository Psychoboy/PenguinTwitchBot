using PenguinTwitchBot.Application.TTS;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.Commands.TTS;
using PenguinTwitchBot.Bot.Alerts;
using NSubstitute;
using Xunit;
using PenguinTwitchBot.Database.Bot.Models;

namespace PenguinTwitchBot.Test.Application.TTS
{
    public class TTSCreateHandlerTests
    {
        [Fact]
        public async Task Handle_WithValidFileName_QueuesAlert()
        {
            var ttsPlayerService = Substitute.For<ITTSPlayerService>();
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<TTSCreateHandler>>();

            var voice = new RegisteredVoice 
            { 
                Name = "TestVoice", 
                Type = RegisteredVoice.VoiceType.Windows 
            };
            var ttsRequest = new TTSRequest 
            { 
                Message = "Hello", 
                RegisteredVoice = voice 
            };
            var notification = new TTSCreateNotification(ttsRequest);
            ttsPlayerService.CreateTTSFile(ttsRequest).Returns("output.mp3");

            var handler = new TTSCreateHandler(ttsPlayerService, webSocketMessenger, logger);
            await handler.Handle(notification, CancellationToken.None);

            await ttsPlayerService.Received(1).CreateTTSFile(ttsRequest);
            await webSocketMessenger.Received(1).AddToQueue(Arg.Any<string>());
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(TTSCreateHandler).GetMethod("Handle"));
        }

        [Fact]
        public void TTSCreateNotification_PropertyExists()
        {
            var ttsRequest = new TTSRequest { Message = "test" };
            var notification = new TTSCreateNotification(ttsRequest);
            Assert.NotNull(notification.TTSRequest);
        }
    }
}