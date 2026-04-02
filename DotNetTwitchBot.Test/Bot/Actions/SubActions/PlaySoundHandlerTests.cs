using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class PlaySoundHandlerTests
    {
        [Fact]
        public async Task ValidPlaySoundType_PublishesQueueAlert()
        {
            // Arrange
            var mediator = Substitute.For<IMediator>();
            var logger = Substitute.For<ILogger<PlaySoundHandler>>();
            var handler = new PlaySoundHandler(mediator, logger);

            var playSoundType = new PlaySoundType
            {
                File = "%sound_file%"
            };

            var variables = new Dictionary<string, string> { { "sound_file", "sound.mp3" } };

            // Act
            await handler.ExecuteAsync(playSoundType, variables);

            // Assert
            await mediator.Received(1).Publish(Arg.Is<QueueAlert>(q =>
                q.Alert.Contains("sound.mp3")));
            Assert.Equal("sound.mp3", playSoundType.File);
        }

        [Fact]
        public async Task WrongType_LogsWarning()
        {
            // Arrange
            var mediator = Substitute.For<IMediator>();
            var logger = Substitute.For<ILogger<PlaySoundHandler>>();
            var handler = new PlaySoundHandler(mediator, logger);

            var wrongType = new SendMessageType();
            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(wrongType, variables);

            // Assert
            logger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("is not of PlaySoundType class")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
