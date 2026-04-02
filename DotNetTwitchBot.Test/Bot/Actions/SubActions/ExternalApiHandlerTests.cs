using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class ExternalApiHandlerTests
    {
        // Note: Skipping actual HTTP test as it would require network access
        // The handler creates its own HttpClient internally

        [Fact]
        public async Task WrongType_LogsError()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ExternalApiHandler>>();
            var handler = new ExternalApiHandler(logger);

            var wrongType = new SubActionType();
            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(wrongType, variables);

            // Assert
            logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Invalid sub action type for ExternalApiHandler")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task InvalidUrl_LogsError()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ExternalApiHandler>>();
            var handler = new ExternalApiHandler(logger);

            var externalApiType = new ExternalApiType
            {
                Text = "invalid-url",
                HttpMethod = "GET"
            };

            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(externalApiType, variables);

            // Assert
            logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Error executing ExternalApiHandler")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
