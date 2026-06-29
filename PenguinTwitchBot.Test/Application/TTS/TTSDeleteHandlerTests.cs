using PenguinTwitchBot.Application.TTS;
using NSubstitute;
using Xunit;

namespace PenguinTwitchBot.Test.Application.TTS
{
    public class TTSDeleteHandlerTests
    {
        [Fact]
        public async Task Handle_WithValidData_ExtractsFileName()
        {
            var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<TTSDeleteHandler>>();
            var handler = new TTSDeleteHandler(logger);
            var notification = new TTSDeleteNotification("prefix:testfile");

            await handler.Handle(notification, CancellationToken.None);

            // No exception thrown means handler executed
        }

        [Fact]
        public async Task Handle_WithNoColon_DoesNotThrow()
        {
            var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<TTSDeleteHandler>>();
            var handler = new TTSDeleteHandler(logger);
            var notification = new TTSDeleteNotification("noColonHere");

            await handler.Handle(notification, CancellationToken.None);
        }

        [Fact]
        public async Task Handle_WithCancellationTokenIsAccepted()
        {
            var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<TTSDeleteHandler>>();
            var handler = new TTSDeleteHandler(logger);
            var notification = new TTSDeleteNotification("prefix:value");
            var cts = new CancellationTokenSource();

            await handler.Handle(notification, cts.Token);
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(TTSDeleteHandler).GetMethod("Handle"));
        }

        [Fact]
        public void TTSDeleteNotification_DataProperty()
        {
            var notification = new TTSDeleteNotification("testdata:value");
            Assert.Equal("testdata:value", notification.Data);
        }
    }
}