using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class SetVariableHandlerTests
    {
        [Fact]
        public async Task ValidSetVariableType_SetsVariable()
        {
            // Arrange
            var handler = new SetVariableHandler();

            var setVariableType = new SetVariableType
            {
                Text = "my_var",
                Value = "test_value"
            };

            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(setVariableType, variables);

            // Assert
            Assert.True(variables.ContainsKey("my_var"));
            Assert.Equal("test_value", variables["my_var"]);
        }

        [Fact]
        public async Task SetVariable_WithVariableReplacement_ReplacesVariables()
        {
            // Arrange
            var handler = new SetVariableHandler();

            var setVariableType = new SetVariableType
            {
                Text = "greeting",
                Value = "Hello %user%!"
            };

            var variables = new Dictionary<string, string> { { "user", "TestUser" } };

            // Act
            await handler.ExecuteAsync(setVariableType, variables);

            // Assert
            Assert.Equal("Hello TestUser!", variables["greeting"]);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var handler = new SetVariableHandler();

            var wrongType = new SendMessageType();
            var variables = new Dictionary<string, string>();

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => handler.ExecuteAsync(wrongType, variables));
        }
    }
}
