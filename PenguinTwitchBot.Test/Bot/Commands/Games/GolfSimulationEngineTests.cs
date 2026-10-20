using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Commands.Games.Golf;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Games
{
    public class GolfSimulationEngineTests
    {
        [Theory]
        // Sunk in the cup -> 100 pts, bullseye, win
        [InlineData(0.84, 0.84, 1, 10, false, 100, 100, 0, true, true)]
        // Sunk in the cup on stroke 2 with 10 pt penalty -> 100 base, 10 penalty, 90 final
        [InlineData(0.84, 0.84, 2, 10, false, 100, 90, 10, true, true)]
        // Sunk in the cup on stroke 3 with 15 pt penalty -> 100 base, 30 penalty, 70 final
        [InlineData(0.84, 0.84, 3, 15, false, 100, 70, 30, true, true)]
        // Lip-out resting inside cup radius (e.g. at 0.845 when isLipOut = true) -> 95 base pts, not bullseye
        [InlineData(0.845, 0.84, 1, 10, true, 95, 95, 0, false, true)]
        // Right outside the cup lip edge (0.84 + 0.0145) -> 95 base pts
        [InlineData(0.8545, 0.84, 1, 10, false, 95, 95, 0, false, true)]
        // Outer fringe of putting green (0.84 + 0.125) -> 10 base pts
        [InlineData(0.965, 0.84, 1, 10, false, 10, 10, 0, false, true)]
        // Off the green (short on fairway at 0.50) -> 0 pts
        [InlineData(0.50, 0.84, 1, 10, false, 0, 0, 0, false, false)]
        public void CalculateScore_OptionBProximityAndStrokePenalty_ReturnsExpectedValues(
            double restPos,
            double targetPos,
            int strokeNumber,
            int strokePenaltyPoints,
            bool isLipOut,
            int expectedBaseScore,
            int expectedFinalScore,
            int expectedPenalty,
            bool expectedBullseye,
            bool expectedWin)
        {
            var (baseScore, finalScore, penalty, isBullseye, isWin) = GolfSimulationEngine.CalculateScore(
                restPos,
                targetPos,
                strokeNumber,
                strokePenaltyPoints,
                isLipOut);

            Assert.Equal(expectedBaseScore, baseScore);
            Assert.Equal(expectedFinalScore, finalScore);
            Assert.Equal(expectedPenalty, penalty);
            Assert.Equal(expectedBullseye, isBullseye);
            Assert.Equal(expectedWin, isWin);
        }

        [Fact]
        public void CalculateScore_ProximityOnGreen_ScalesSmoothlyBetween10And95()
        {
            var (nearCupScore, _, _, nearBullseye, nearWin) = GolfSimulationEngine.CalculateScore(
                restPos: 0.84 + 0.025,
                targetPos: 0.84,
                strokeNumber: 1,
                strokePenaltyPoints: 10);

            var (midGreenScore, _, _, midBullseye, midWin) = GolfSimulationEngine.CalculateScore(
                restPos: 0.84 + 0.070,
                targetPos: 0.84,
                strokeNumber: 1,
                strokePenaltyPoints: 10);

            var (fringeScore, _, _, fringeBullseye, fringeWin) = GolfSimulationEngine.CalculateScore(
                restPos: 0.84 + 0.120,
                targetPos: 0.84,
                strokeNumber: 1,
                strokePenaltyPoints: 10);

            Assert.InRange(nearCupScore, 80, 95);
            Assert.InRange(midGreenScore, 40, 75);
            Assert.InRange(fringeScore, 10, 30);
            Assert.True(nearCupScore > midGreenScore);
            Assert.True(midGreenScore > fringeScore);
            Assert.False(nearBullseye);
            Assert.False(midBullseye);
            Assert.False(fringeBullseye);
            Assert.True(nearWin);
            Assert.True(midWin);
            Assert.True(fringeWin);
        }

        [Fact]
        public void SimulateGolfShot_ChipShot_HasParabolicFlightArcAndGroundRoll()
        {
            var settings = new GolfGameSettings
            {
                StripPosition = CurlingStripPosition.Bottom,
                SlideDirection = CurlingSlideDirection.LeftToRight,
                PowerVariancePercent = 0.0
            };

            var record = CreateTestRecord("golf-chip-1", strokeNumber: 1);

            GolfSimulationEngine.SimulateGolfShot(
                record,
                settings,
                rawPower: 70.0,
                condition: GolfGreenCondition.StandardGreen,
                targetDistancePercent: 84.0,
                strokeNumber: 1,
                startAxisPosOverride: 0.05,
                rng: new Random(42));

            Assert.NotEmpty(record.Keyframes);
            // Tee chip shot should have airborne keyframes (Z > 0) followed by ground roll (Z == 0)
            Assert.Contains(record.Keyframes, k => k.Z > 0.01);
            Assert.Equal(0.0, record.Keyframes[^1].Z);
            Assert.True(record.LandingX > 0.50);
        }

        [Fact]
        public void SimulateGolfShot_FastShotAcrossCup_TriggersLipOut()
        {
            var settings = new GolfGameSettings
            {
                StripPosition = CurlingStripPosition.Bottom,
                SlideDirection = CurlingSlideDirection.LeftToRight,
                PowerVariancePercent = 0.0
            };

            var record = CreateTestRecord("golf-lipout-1", strokeNumber: 2);

            // Start close to the pin (0.76 -> pin at 0.84) with high power (95) on FastLinks
            // so the putt rolls across the cup faster than MaxCaptureSpeed (0.28)
            GolfSimulationEngine.SimulateGolfShot(
                record,
                settings,
                rawPower: 95.0,
                condition: GolfGreenCondition.FastLinks,
                targetDistancePercent: 84.0,
                strokeNumber: 2,
                startAxisPosOverride: 0.76,
                rng: new Random(42));

            Assert.True(record.IsLipOut);
            Assert.False(record.IsBullseye);
        }

        [Fact]
        public void SimulateGolfShot_PopulatesPendingDropRecordWithFlightAndScoring()
        {
            var settings = new GolfGameSettings
            {
                StripPosition = CurlingStripPosition.Bottom,
                SlideDirection = CurlingSlideDirection.LeftToRight,
                PowerVariancePercent = 0.0,
                MaxStrokesPerPlayer = 3,
                StrokePenaltyPoints = 10
            };

            var record = CreateTestRecord("golf-test-1", strokeNumber: 2);

            GolfSimulationEngine.SimulateGolfShot(
                record,
                settings,
                rawPower: 72.0,
                condition: GolfGreenCondition.StandardGreen,
                targetDistancePercent: 84.0,
                strokeNumber: 2,
                rng: new Random(42));

            Assert.NotEmpty(record.Keyframes);
            Assert.True(record.TotalTicks > 0);
            Assert.True(record.LandingX > 0.05);
            Assert.Equal(2, record.StrokeNumber);
        }

        [Fact]
        public async Task ParsePowerAndEmote_WithPowerAndEmoteFragment_ResolvesEmoteFragmentUrl()
        {
            var eventArgs = new PenguinTwitchBot.Bot.Events.Chat.CommandEventArgs
            {
                Command = "golf",
                Arg = "85 sptvHello",
                Args = ["85", "sptvHello"],
                Fragments =
                [
                    new PenguinTwitchBot.Bot.Models.Chat.ChatOverlayFragment
                    {
                        Type = "text",
                        Text = "!golf 85 "
                    },
                    new PenguinTwitchBot.Bot.Models.Chat.ChatOverlayFragment
                    {
                        Type = "emote",
                        Text = "sptvHello",
                        EmoteId = "emotesv2_12345",
                        EmoteUrl = "https://static-cdn.jtvnw.net/emoticons/v2/emotesv2_12345/animated/dark/1.0"
                    }
                ]
            };

            // Both direct command (explicitEmote: null) and SubAction (%Args% -> explicitEmote: "85 sptvHello") must extract "sptvHello"
            var (power1, emoteToken1) = await DropGameMediaResolver.ParsePowerAndEmoteAsync(
                emoteService: null,
                eventArgs: eventArgs,
                explicitPower: null,
                explicitEmote: null,
                enableViewerPowerControl: true,
                defaultPowerMin: 40,
                defaultPowerMax: 95);

            Assert.Equal(85.0, power1);
            Assert.Equal("sptvHello", emoteToken1);

            var (power2, emoteToken2) = await DropGameMediaResolver.ParsePowerAndEmoteAsync(
                emoteService: null,
                eventArgs: eventArgs,
                explicitPower: null,
                explicitEmote: "85 sptvHello",
                enableViewerPowerControl: true,
                defaultPowerMin: 40,
                defaultPowerMax: 95);

            Assert.Equal(85.0, power2);
            Assert.Equal("sptvHello", emoteToken2);

            var decalUrl = await DropGameMediaResolver.ResolveCharacterOrEmoteUrlAsync(
                emoteService: null,
                eventArgs: eventArgs,
                emoteOrImageInput: emoteToken2,
                fallbackUrl: null,
                allowDirectUrlOrPath: false,
                fallbackToEventArgsArg: false);

            Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/emotesv2_12345/animated/dark/1.0", decalUrl);
        }

        private static PendingDropRecord CreateTestRecord(string token, int strokeNumber)
        {
            var now = DateTime.UtcNow;
            return new PendingDropRecord
            {
                DropToken = token,
                GameType = DropGameType.Golf,
                Username = "golfer",
                DisplayName = "Golfer",
                UserId = "user-golf-1",
                StrokeNumber = strokeNumber,
                EventArgs = new PenguinTwitchBot.Bot.Events.Chat.CommandEventArgs
                {
                    Command = "golf",
                    Name = "golfer",
                    DisplayName = "Golfer",
                    UserId = "user-golf-1"
                },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now,
                EarliestCompletionUtc = now,
                ExpiresAtUtc = now.AddMinutes(1)
            };
        }
    }
}
