using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class BreakHandlerTests
    {
        [Fact]
        public async Task ValidBreakType_ThrowsBreakException()
        {
            // Arrange
            var handler = new BreakHandler();

            var breakType = new BreakType();
            var variables = new Dictionary<string, string>();

            // Act & Assert
            await Assert.ThrowsAsync<BreakException>(
                () => handler.ExecuteAsync(breakType, variables));
        }

        [Fact]
        public async Task WrongType_ThrowsSubActionHandlerException()
        {
            // Arrange
            var handler = new BreakHandler();

            var wrongType = new SendMessageType();
            var variables = new Dictionary<string, string>();

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => handler.ExecuteAsync(wrongType, variables));
        }
    }
}
