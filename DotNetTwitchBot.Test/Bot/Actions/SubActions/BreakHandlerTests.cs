using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using Moq;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class BreakHandlerTests
    {
        [Fact]
        public async Task ValidBreakType_ThrowsBreakException()
        {
            // Arrange
            var mockContextAccessor = new Mock<ISubActionExecutionContextAccessor>();
            var handler = new BreakHandler(mockContextAccessor.Object);

            var breakType = new BreakType();
            var variables = new ConcurrentDictionary<string, string>();

            // Act & Assert
            await Assert.ThrowsAsync<BreakException>(
                () => handler.ExecuteAsync(breakType, variables));
        }

        [Fact]
        public async Task WrongType_ThrowsSubActionHandlerException()
        {
            // Arrange
            var mockContextAccessor = new Mock<ISubActionExecutionContextAccessor>();
            var handler = new BreakHandler(mockContextAccessor.Object);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => handler.ExecuteAsync(wrongType, variables));
        }
    }
}
