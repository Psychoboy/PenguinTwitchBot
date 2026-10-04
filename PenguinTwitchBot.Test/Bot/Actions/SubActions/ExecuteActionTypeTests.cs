using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class ExecuteActionTypeTests
    {
        [Fact]
        public void GetUIFields_ReturnsExpectedFields()
        {
            var subAction = new ExecuteActionType();
            var fields = subAction.GetUIFields();

            Assert.Contains(fields, f => f.PropertyName == nameof(ExecuteActionType.ActionId));
            Assert.Contains(fields, f => f.PropertyName == nameof(ExecuteActionType.Text));
            Assert.Contains(fields, f => f.PropertyName == nameof(ExecuteActionType.ElevatedCommand));
            Assert.Contains(fields, f => f.PropertyName == nameof(ExecuteActionType.RankToExecuteAs));
            Assert.Contains(fields, f => f.PropertyName == nameof(ExecuteActionType.Enabled));
            Assert.Contains(fields, f => f.PropertyName == "info_hint");
        }

        [Fact]
        public void GetValues_And_SetValues_RoundTrip()
        {
            var original = new ExecuteActionType
            {
                ActionId = 42,
                ActionName = "TestAction",
                Text = "arg1 arg2",
                ElevatedCommand = true,
                RankToExecuteAs = "Moderator",
                Enabled = false
            };

            var values = original.GetValues();

            var restored = new ExecuteActionType();
            restored.SetValues(values);

            Assert.Equal(original.ActionId, restored.ActionId);
            Assert.Equal(original.ActionName, restored.ActionName);
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
            var subAction = new ExecuteActionType();
            var dict = new Dictionary<string, object?>
            {
                [nameof(ExecuteActionType.ActionId)] = "10",
                [nameof(ExecuteActionType.ElevatedCommand)] = elevatedValue
            };

            subAction.SetValues(dict);

            Assert.Equal(expected, subAction.ElevatedCommand);
        }

        [Fact]
        public void SetValues_MissingElevatedCommand_KeepsDefault()
        {
            var subAction = new ExecuteActionType { ElevatedCommand = false };
            subAction.SetValues(new Dictionary<string, object?> { [nameof(ExecuteActionType.ActionId)] = "10" });
            Assert.False(subAction.ElevatedCommand);
        }

        [Fact]
        public void Validate_MissingActionId_ReturnsError()
        {
            var subAction = new ExecuteActionType();
            var result = subAction.Validate(new Dictionary<string, object?>());

            Assert.Equal("Action to Execute is required", result);
        }

        [Fact]
        public void Validate_InvalidActionId_ReturnsError()
        {
            var subAction = new ExecuteActionType();
            var result = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(ExecuteActionType.ActionId)] = "0"
            });

            Assert.Equal("Action to Execute is required", result);
        }

        [Fact]
        public void Validate_ElevatedCommandTrue_MissingRank_ReturnsError()
        {
            var subAction = new ExecuteActionType();
            var result = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(ExecuteActionType.ActionId)] = "1",
                [nameof(ExecuteActionType.ElevatedCommand)] = true,
                [nameof(ExecuteActionType.RankToExecuteAs)] = ""
            });

            Assert.Equal("Rank to Execute As is required when Elevated Rank is enabled", result);
        }

        [Fact]
        public void Validate_ElevatedCommandStringTrue_MissingRank_ReturnsError()
        {
            var subAction = new ExecuteActionType();
            var result = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(ExecuteActionType.ActionId)] = "1",
                [nameof(ExecuteActionType.ElevatedCommand)] = "true",
                [nameof(ExecuteActionType.RankToExecuteAs)] = ""
            });

            Assert.Equal("Rank to Execute As is required when Elevated Rank is enabled", result);
        }

        [Fact]
        public void Validate_ValidElevatedCommand_ReturnsNull()
        {
            var subAction = new ExecuteActionType();
            var result = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(ExecuteActionType.ActionId)] = "1",
                [nameof(ExecuteActionType.ElevatedCommand)] = "true",
                [nameof(ExecuteActionType.RankToExecuteAs)] = "Moderator"
            });

            Assert.Null(result);
        }

        [Fact]
        public void Validate_ValidNonElevatedCommand_ReturnsNull()
        {
            var subAction = new ExecuteActionType();
            var result = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(ExecuteActionType.ActionId)] = "1",
                [nameof(ExecuteActionType.ElevatedCommand)] = false
            });

            Assert.Null(result);
        }
    }
}
