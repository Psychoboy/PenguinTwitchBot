using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class ExecuteCommandTypeTests
    {
        [Fact]
        public void GetUIFields_ReturnsExpectedFields()
        {
            var subAction = new ExecuteCommandType();
            var fields = subAction.GetUIFields();

            Assert.Contains(fields, f => f.PropertyName == nameof(ExecuteCommandType.CommandName));
            Assert.Contains(fields, f => f.PropertyName == nameof(ExecuteCommandType.Text));
            Assert.Contains(fields, f => f.PropertyName == nameof(ExecuteCommandType.ElevatedCommand));
            Assert.Contains(fields, f => f.PropertyName == nameof(ExecuteCommandType.RankToExecuteAs));
            Assert.Contains(fields, f => f.PropertyName == nameof(ExecuteCommandType.Enabled));
            Assert.Contains(fields, f => f.PropertyName == "info_hint");
        }

        [Fact]
        public void GetValues_And_SetValues_RoundTrip()
        {
            var original = new ExecuteCommandType
            {
                CommandName = "shoutout",
                Text = "arg1 arg2",
                ElevatedCommand = true,
                RankToExecuteAs = "Moderator",
                Enabled = false
            };

            var values = original.GetValues();

            var restored = new ExecuteCommandType();
            restored.SetValues(values);

            Assert.Equal(original.CommandName, restored.CommandName);
            Assert.Equal(original.Text, restored.Text);
            Assert.True(restored.ElevatedCommand);
            Assert.Equal(original.RankToExecuteAs, restored.RankToExecuteAs);
            Assert.False(restored.Enabled);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        [InlineData("true", true)]
        [InlineData("True", true)]
        [InlineData("TRUE", true)]
        [InlineData("false", false)]
        [InlineData("False", false)]
        [InlineData("invalid", false)]
        [InlineData(null, false)]
        public void SetValues_ElevatedCommand_ParsesCorrectly(object? elevatedValue, bool expected)
        {
            var subAction = new ExecuteCommandType();
            var dict = new Dictionary<string, object?>
            {
                [nameof(ExecuteCommandType.CommandName)] = "test",
                [nameof(ExecuteCommandType.ElevatedCommand)] = elevatedValue
            };

            subAction.SetValues(dict);

            Assert.Equal(expected, subAction.ElevatedCommand);
        }

        [Fact]
        public void SetValues_MissingElevatedCommand_KeepsDefault()
        {
            var subAction = new ExecuteCommandType { ElevatedCommand = false };
            subAction.SetValues(new Dictionary<string, object?> { [nameof(ExecuteCommandType.CommandName)] = "test" });
            Assert.False(subAction.ElevatedCommand);
        }

        [Fact]
        public void Validate_MissingCommandName_ReturnsError()
        {
            var subAction = new ExecuteCommandType();
            var result = subAction.Validate(new Dictionary<string, object?>());

            Assert.Equal("Command to Execute is required", result);
        }

        [Fact]
        public void Validate_WhitespaceCommandName_ReturnsError()
        {
            var subAction = new ExecuteCommandType();
            var result = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(ExecuteCommandType.CommandName)] = "   "
            });

            Assert.Equal("Command to Execute is required", result);
        }

        [Fact]
        public void Validate_ElevatedCommandTrue_MissingRank_ReturnsError()
        {
            var subAction = new ExecuteCommandType();
            var result = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(ExecuteCommandType.CommandName)] = "test",
                [nameof(ExecuteCommandType.ElevatedCommand)] = true,
                [nameof(ExecuteCommandType.RankToExecuteAs)] = ""
            });

            Assert.Equal("Rank to Execute As is required when Elevated Command is enabled", result);
        }

        [Fact]
        public void Validate_ElevatedCommandStringTrue_MissingRank_ReturnsError()
        {
            var subAction = new ExecuteCommandType();
            var result = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(ExecuteCommandType.CommandName)] = "test",
                [nameof(ExecuteCommandType.ElevatedCommand)] = "true",
                [nameof(ExecuteCommandType.RankToExecuteAs)] = ""
            });

            Assert.Equal("Rank to Execute As is required when Elevated Command is enabled", result);
        }

        [Fact]
        public void Validate_ValidElevatedCommand_ReturnsNull()
        {
            var subAction = new ExecuteCommandType();
            var result = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(ExecuteCommandType.CommandName)] = "test",
                [nameof(ExecuteCommandType.ElevatedCommand)] = "true",
                [nameof(ExecuteCommandType.RankToExecuteAs)] = "Moderator"
            });

            Assert.Null(result);
        }

        [Fact]
        public void Validate_ValidNonElevatedCommand_ReturnsNull()
        {
            var subAction = new ExecuteCommandType();
            var result = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(ExecuteCommandType.CommandName)] = "test",
                [nameof(ExecuteCommandType.ElevatedCommand)] = false
            });

            Assert.Null(result);
        }
    }
}
