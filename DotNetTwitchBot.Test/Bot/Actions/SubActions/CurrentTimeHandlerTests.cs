using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class CurrentTimeHandlerTests
    {
        [Fact]
        public async Task ValidType_SetsCurrentTimeVariable()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CurrentTimeHandler>>();
            var handler = new CurrentTimeHandler(logger);

            var currentTimeType = new CurrentTimeType();
            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(currentTimeType, variables);

            // Assert
            Assert.True(variables.ContainsKey("current_time"));
            Assert.NotEmpty(variables["current_time"]);
            // Verify it's in the expected time format (h:mm:ss tt)
            var timePattern = @"^\d{1,2}:\d{2}:\d{2} (AM|PM)$";
            Assert.Matches(timePattern, variables["current_time"]);
        }

        [Fact]
        public async Task WrongType_LogsWarning()
        {
            // Arrange
            var logger = Substitute.For<ILogger<CurrentTimeHandler>>();
            var handler = new CurrentTimeHandler(logger);

            var wrongType = new SendMessageType();
            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(wrongType, variables);

            // Assert
            logger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("is not of CurrentTimeType class")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
