using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class RandomIntHandlerTests
    {
        [Fact]
        public async Task ValidRange_SetsRandomIntVariable()
        {
            // Arrange
            var handler = new RandomIntHandler();

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
            var handler = new RandomIntHandler();

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
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var handler = new RandomIntHandler();

            var wrongType = new SendMessageType();
            var variables = new Dictionary<string, string>();

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => handler.ExecuteAsync(wrongType, variables));
        }
    }
}
