using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Commands.Games.Curling;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Games
{
    public class CurlingSimulationEngineTests
    {
        [Theory]
        [InlineData(CurlingIceCondition.Slick, 0.78)]
        [InlineData(CurlingIceCondition.Normal, 1.00)]
        [InlineData(CurlingIceCondition.HeavyFrost, 1.28)]
        [InlineData(CurlingIceCondition.CrossDrift, 1.05)]
        public void ResolveIceDragMultiplier_ReturnsExpectedMultipliers(CurlingIceCondition condition, double expected)
        {
            var result = CurlingSimulationEngine.ResolveIceDragMultiplier(condition);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CalculateEffectivePower_AppliesVarianceWithinRange()
        {
            var rng = new Random(42);
            for (int i = 0; i < 50; i++)
            {
                var effective = CurlingSimulationEngine.CalculateEffectivePower(75.0, 3.0, rng);
                Assert.InRange(effective, 72.0, 78.0);
            }
        }

        [Fact]
        public void CalculateEffectivePower_WhenVarianceZero_ReturnsExactPower()
        {
            var rng = new Random(42);
            for (int i = 0; i < 20; i++)
            {
                var effective = CurlingSimulationEngine.CalculateEffectivePower(78.5, 0.0, rng);
                Assert.Equal(78.5, effective);
            }
        }

        [Fact]
        public void CalculateEffectivePower_VarianceProducesSpreadAcrossIdenticalRawInputs()
        {
            var rng = new Random(12345);
            var results = new HashSet<double>();
            for (int i = 0; i < 25; i++)
            {
                var effective = CurlingSimulationEngine.CalculateEffectivePower(80.0, 3.0, rng);
                results.Add(Math.Round(effective, 4));
            }
            // 25 random throws should produce many distinct effective powers rather than collapsing to a single value
            Assert.True(results.Count > 15, $"Expected spread in power, got {results.Count} unique values");
        }

        [Fact]
        public void PowerToInitialSpeed_ScalesWithPower()
        {
            var lowSpeed = CurlingSimulationEngine.PowerToInitialSpeed(50.0, 1.0);
            var highSpeed = CurlingSimulationEngine.PowerToInitialSpeed(100.0, 1.0);

            Assert.True(highSpeed > lowSpeed);
            Assert.True(lowSpeed > 0);
        }

        [Fact]
        public void CalculateSlideDistance_SlickSlidesFurtherThanHeavyFrost()
        {
            var speed = 0.50;
            var slickDist = CurlingSimulationEngine.CalculateSlideDistance(speed, 0.78);
            var normalDist = CurlingSimulationEngine.CalculateSlideDistance(speed, 1.00);
            var frostDist = CurlingSimulationEngine.CalculateSlideDistance(speed, 1.28);

            Assert.True(slickDist > normalDist);
            Assert.True(normalDist > frostDist);
        }

        [Theory]
        // Exact button bullseye
        [InlineData(0.84, 0.84, 100, true, true)]
        // Within button (< 0.022)
        [InlineData(0.85, 0.84, 100, true, true)]
        // Within 4-foot (< 0.048)
        [InlineData(0.88, 0.84, 75, false, true)]
        // Within 8-foot (< 0.082)
        [InlineData(0.91, 0.84, 50, false, true)]
        // Within 12-foot (< 0.125)
        [InlineData(0.95, 0.84, 25, false, true)]
        // Outside 12-foot
        [InlineData(0.50, 0.84, 0, false, false)]
        public void CalculateScore_CurlingHouse_ReturnsExpectedRings(
            double restPos, double targetPos, int expectedScore, bool expectedBullseye, bool expectedWin)
        {
            var (score, isBullseye, isWin) = CurlingSimulationEngine.CalculateScore(restPos, targetPos, CurlingScoringType.CurlingHouse);

            Assert.Equal(expectedScore, score);
            Assert.Equal(expectedBullseye, isBullseye);
            Assert.Equal(expectedWin, isWin);
        }

        [Theory]
        // Zone 3: center of house (delta <= 0.035)
        [InlineData(0.84, 0.84, 30, true, true)]
        // Zone 2: mid distance (0.035 < delta <= 0.085)
        [InlineData(0.78, 0.84, 20, false, true)]
        // Zone 1: outer distance (0.085 < delta <= 0.140)
        [InlineData(0.72, 0.84, 10, false, true)]
        // Overshot Kitchen penalty (restPos > targetPos + 0.035)
        [InlineData(0.90, 0.84, -10, false, false)]
        // Short miss
        [InlineData(0.40, 0.84, 0, false, false)]
        public void CalculateScore_Shuffleboard_ReturnsExpectedZones(
            double restPos, double targetPos, int expectedScore, bool expectedBullseye, bool expectedWin)
        {
            var (score, isBullseye, isWin) = CurlingSimulationEngine.CalculateScore(restPos, targetPos, CurlingScoringType.Shuffleboard);

            Assert.Equal(expectedScore, score);
            Assert.Equal(expectedBullseye, isBullseye);
            Assert.Equal(expectedWin, isWin);
        }

        [Fact]
        public void CalculateScore_Shuffleboard_WhenKitchenPenaltyDisabled_ReturnsZero()
        {
            // Overshot position where penalty normally triggers (-10)
            const double restPos = 0.90;
            const double targetPos = 0.84;

            var (scoreWithPenalty, _, _) = CurlingSimulationEngine.CalculateScore(
                restPos, targetPos, CurlingScoringType.Shuffleboard, enableKitchenPenalty: true);
            var (scoreWithoutPenalty, isBullseye, isWin) = CurlingSimulationEngine.CalculateScore(
                restPos, targetPos, CurlingScoringType.Shuffleboard, enableKitchenPenalty: false);

            Assert.Equal(-10, scoreWithPenalty);
            Assert.Equal(0, scoreWithoutPenalty);
            Assert.False(isBullseye);
            Assert.False(isWin);
        }

        [Fact]
        public void SimulateCurlingSlide_PopulatesRecordKeyframesAndScore()
        {
            var settings = new CurlingGameSettings
            {
                StripPosition = CurlingStripPosition.Bottom,
                SlideDirection = CurlingSlideDirection.LeftToRight,
                ScoringType = CurlingScoringType.CurlingHouse,
                PowerVariancePercent = 0.0
            };

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "curl-test-1",
                GameType = DropGameType.Curling,
                Username = "curler",
                DisplayName = "Curler",
                UserId = "user-123",
                EventArgs = new PenguinTwitchBot.Bot.Events.Chat.CommandEventArgs { Command = "curl", Name = "curler", DisplayName = "Curler", UserId = "user-123" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now,
                EarliestCompletionUtc = now,
                ExpiresAtUtc = now.AddMinutes(1)
            };

            CurlingSimulationEngine.SimulateCurlingSlide(
                record,
                settings,
                rawPower: 85.0,
                condition: CurlingIceCondition.Normal,
                targetDistancePercent: 84.0);

            Assert.NotEmpty(record.Keyframes);
            Assert.True(record.TotalTicks > 0);
            Assert.True(record.LandingX > 0.05);
        }

        [Fact]
        public void SimulateCurlingSlide_ReversedDirection_StartsNearEndAndSlidesBackward()
        {
            var settings = new CurlingGameSettings
            {
                StripPosition = CurlingStripPosition.Bottom,
                SlideDirection = CurlingSlideDirection.RightToLeft,
                ScoringType = CurlingScoringType.CurlingHouse,
                PowerVariancePercent = 0.0
            };

            var now = DateTime.UtcNow;
            var record = new PendingDropRecord
            {
                DropToken = "curl-test-rev",
                GameType = DropGameType.Curling,
                Username = "curler",
                DisplayName = "Curler",
                UserId = "user-123",
                EventArgs = new PenguinTwitchBot.Bot.Events.Chat.CommandEventArgs { Command = "curl", Name = "curler", DisplayName = "Curler", UserId = "user-123" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now,
                EarliestCompletionUtc = now,
                ExpiresAtUtc = now.AddMinutes(1)
            };

            CurlingSimulationEngine.SimulateCurlingSlide(
                record,
                settings,
                rawPower: 60.0,
                condition: CurlingIceCondition.Normal,
                targetDistancePercent: 80.0);

            Assert.NotEmpty(record.Keyframes);
            // In reversed mode, start X is at 0.95 and Vx is negative
            Assert.Equal(0.95, record.Keyframes[0].X);
            Assert.True(record.Keyframes[0].Vx < 0);
            // Rest position is < 0.95
            Assert.True(record.LandingX < 0.95);
        }

        [Theory]
        // In reversed mode, target is at 0.20 (1.0 - 0.80).
        // Apex Zone 3: closest to target (|delta| <= 0.035)
        [InlineData(0.20, 0.20, 30, true, true)]
        // Zone 2: approach side (restPos = 0.25, delta = 0.05)
        [InlineData(0.25, 0.20, 20, false, true)]
        // Zone 1: approach side (restPos = 0.30, delta = 0.10)
        [InlineData(0.30, 0.20, 10, false, true)]
        // Overshot Kitchen penalty in reverse: restPos < targetPos - 0.035 => restPos = 0.15
        [InlineData(0.15, 0.20, -10, false, false)]
        public void CalculateScore_Shuffleboard_Reversed_ReturnsExpectedZonesAndKitchen(
            double restPos, double targetPos, int expectedScore, bool expectedBullseye, bool expectedWin)
        {
            var (score, isBullseye, isWin) = CurlingSimulationEngine.CalculateScore(restPos, targetPos, CurlingScoringType.Shuffleboard, isReversed: true);

            Assert.Equal(expectedScore, score);
            Assert.Equal(expectedBullseye, isBullseye);
            Assert.Equal(expectedWin, isWin);
        }
    }
}

