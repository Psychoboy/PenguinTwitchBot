using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using Moq;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class ExternalApiHandlerTests
    {
        // Note: Skipping actual HTTP test as it would require network access
        // The handler creates its own HttpClient internally

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var mockContextAccessor = new Mock<ISubActionExecutionContextAccessor>();
            var handler = new ExternalApiHandler(mockContextAccessor.Object);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task InvalidUrl_ThrowsException()
        {
            // Arrange
            var mockContextAccessor = new Mock<ISubActionExecutionContextAccessor>();
            var handler = new ExternalApiHandler(mockContextAccessor.Object);

            var externalApiType = new ExternalApiType
            {
                Text = "invalid-url",
                HttpMethod = "GET"
            };

            var variables = new ConcurrentDictionary<string, string>();

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => handler.ExecuteAsync(externalApiType, variables));
        }
    }
}
