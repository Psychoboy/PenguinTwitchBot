using PenguinTwitchBot.Bot.Overlay;

namespace PenguinTwitchBot.Test.Bot.Overlay
{
    public class TimerDurationTests
    {
        [Theory]
        [InlineData("90", 90)]
        [InlineData("0", 0)]
        [InlineData("1.5", 1.5)]
        [InlineData("00:01:30", 90)]
        [InlineData("01:00:00", 3600)]
        [InlineData("  120  ", 120)]
        public void TryParse_AcceptsSecondsAndHms(string input, double expected)
        {
            Assert.True(TimerDuration.TryParse(input, out var seconds));
            Assert.Equal(expected, seconds, 3);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("abc")]
        [InlineData("-30")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("-Infinity")]
        public void TryParse_RejectsInvalidValues(string? input)
        {
            Assert.False(TimerDuration.TryParse(input, out _));
        }

        [Theory]
        [InlineData("1e300")]
        [InlineData("999999999999999")]
        public void TryParse_RejectsValuesTooLargeToConvert(string input)
        {
            Assert.False(TimerDuration.TryParse(input, out _));
        }

        [Theory]
        [InlineData(0, "00:00:00")]
        [InlineData(90, "00:01:30")]
        [InlineData(3600, "01:00:00")]
        [InlineData(-5, "00:00:00")]
        public void Format_RendersHoursMinutesSeconds(double seconds, string expected)
        {
            Assert.Equal(expected, TimerDuration.Format(seconds));
        }

        [Fact]
        public void Format_UsesTotalHoursBeyondOneDay()
        {
            // 25 hours must not wrap back to 01:00:00.
            Assert.Equal("25:00:00", TimerDuration.Format(25 * 3600));
        }

        [Fact]
        public void Format_DoesNotThrowForOutOfRangeValues()
        {
            var exception = Record.Exception(() => TimerDuration.Format(double.MaxValue));
            Assert.Null(exception);
        }
    }
}
