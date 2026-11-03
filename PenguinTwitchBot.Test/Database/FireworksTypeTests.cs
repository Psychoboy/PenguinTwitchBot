using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Test.Database
{
    public class FireworksTypeTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("5")]
        [InlineData("2.5")]
        [InlineData("%Size%")]
        [InlineData("$math(%Size% + 1)$")]
        public void Validate_AllowsBlankInRangeAndDynamicSizes(string size)
        {
            var fireworks = new FireworksType();

            Assert.Null(fireworks.Validate(new Dictionary<string, object?> { [nameof(FireworksType.Size)] = size }));
        }

        [Theory]
        [InlineData("-0.1")]
        [InlineData("5.1")]
        [InlineData("NaN")]
        [InlineData("not-a-size")]
        public void Validate_RejectsInvalidOrOutOfRangeStaticSizes(string size)
        {
            var fireworks = new FireworksType();

            Assert.NotNull(fireworks.Validate(new Dictionary<string, object?> { [nameof(FireworksType.Size)] = size }));
        }
    }
}