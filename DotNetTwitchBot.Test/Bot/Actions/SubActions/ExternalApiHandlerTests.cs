using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using Moq;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class ExternalApiHandlerTests
    {
        [Fact]
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var mockContextAccessor = new Mock<ISubActionExecutionContextAccessor>();
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());

            var handler = new ExternalApiHandler(mockContextAccessor.Object, mockHttpClientFactory.Object);

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
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());

            var handler = new ExternalApiHandler(mockContextAccessor.Object, mockHttpClientFactory.Object);

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
