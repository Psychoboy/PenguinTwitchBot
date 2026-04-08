using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class CurrentTimeHandlerTests
    {
        [Fact]
        public async Task ValidType_SetsCurrentTimeVariable()
        {
            // Arrange
            var handler = new CurrentTimeHandler();

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
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var handler = new CurrentTimeHandler();

            var wrongType = new SendMessageType();
            var variables = new Dictionary<string, string>();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<SubActionHandlerException>(
                () => handler.ExecuteAsync(wrongType, variables));

            Assert.Contains("is not of CurrentTimeType class", exception.Message);
        }
    }
}
