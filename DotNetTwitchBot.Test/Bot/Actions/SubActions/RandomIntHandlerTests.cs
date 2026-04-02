using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class RandomIntHandlerTests
    {
        [Fact]
        public async Task ValidRange_SetsRandomIntVariable()
        {
            // Arrange
            var logger = Substitute.For<ILogger<RandomIntHandler>>();
            var handler = new RandomIntHandler(logger);

            var randomIntType = new RandomIntType
            {
                Min = 1,
                Max = 100
            };

            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(randomIntType, variables);

            // Assert
            Assert.True(variables.ContainsKey("random_int"));
            var randomValue = int.Parse(variables["random_int"]);
            Assert.InRange(randomValue, 1, 100);
        }

        [Fact]
        public async Task MinEqualsMax_ReturnsExactValue()
        {
            // Arrange
            var logger = Substitute.For<ILogger<RandomIntHandler>>();
            var handler = new RandomIntHandler(logger);

            var randomIntType = new RandomIntType
            {
                Min = 42,
                Max = 42
            };

            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(randomIntType, variables);

            // Assert
            Assert.Equal("42", variables["random_int"]);
        }

        [Fact]
        public async Task WrongType_LogsWarning()
        {
            // Arrange
            var logger = Substitute.For<ILogger<RandomIntHandler>>();
            var handler = new RandomIntHandler(logger);

            var wrongType = new SendMessageType();
            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(wrongType, variables);

            // Assert
            logger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("RandomIntHandler received unsupported SubActionType")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
