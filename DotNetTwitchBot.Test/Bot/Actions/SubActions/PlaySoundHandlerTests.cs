using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using MediatR;
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
            var handler = new PlaySoundHandler(mediator);

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
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var mediator = Substitute.For<IMediator>();
            var handler = new PlaySoundHandler(mediator);

            var wrongType = new SendMessageType();
            var variables = new Dictionary<string, string>();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<SubActionHandlerException>(
                () => handler.ExecuteAsync(wrongType, variables));

            Assert.Contains("is not of PlaySoundType class", exception.Message);
        }
    }
}
