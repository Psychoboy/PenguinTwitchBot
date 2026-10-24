using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Commands.Games.Golf;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Games
{
    public class GolfSimulationEngineTests
    {
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

            // Start on the green close to the pin (0.82 -> pin at 0.84) with high power (95) on FastLinks
            // so the putt rolls across the cup faster than MaxCaptureSpeed (0.28)
            GolfSimulationEngine.SimulateGolfShot(
                record,
                settings,
                rawPower: 95.0,
                condition: GolfGreenCondition.FastLinks,
                targetDistancePercent: 84.0,
                strokeNumber: 2,
                startAxisPosOverride: 0.82,
                rng: new Random(42));

            Assert.True(record.IsLipOut);
            Assert.False(record.IsBullseye);
        }

        [Fact]
        public void SimulateGolfShot_PopulatesFlightWithoutAuthoritativeOutcome()
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
            Assert.Equal(0, record.Score);
            Assert.Equal(0, record.BaseScore);
            Assert.False(record.IsWin);
            Assert.False(record.IsBullseye);
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

        [Fact]
        public void ResolveClub_CorrectlyIdentifiesDriverWedgeAndPutter()
        {
            // Stroke 1 is always Driver regardless of distance
            Assert.Equal("Driver", GolfSimulationEngine.ResolveClub(1, 0.79));
            Assert.Equal("Driver", GolfSimulationEngine.ResolveClub(1, 0.05));

            // Stroke 2+ off green (> GreenRadius = 0.035) is Wedge (approach chip)
            Assert.Equal("Wedge", GolfSimulationEngine.ResolveClub(2, 0.35));
            Assert.Equal("Wedge", GolfSimulationEngine.ResolveClub(2, 0.05));

            // Stroke 2+ on green (<= GreenRadius = 0.035) is Putter
            Assert.Equal("Putter", GolfSimulationEngine.ResolveClub(2, 0.02));
            Assert.Equal("Putter", GolfSimulationEngine.ResolveClub(3, 0.01));

            // Explicit isOnGreen override from overlay completion takes priority
            Assert.Equal("Putter", GolfSimulationEngine.ResolveClub(2, 0.05, isOnGreen: true));
            Assert.Equal("Wedge", GolfSimulationEngine.ResolveClub(2, 0.01, isOnGreen: false));
        }

        [Fact]
        public void SimulateGolfShot_WedgeChip_TravelsSignificantDistanceAndHasLoft()
        {
            var settings = new GolfGameSettings
            {
                StripPosition = CurlingStripPosition.Bottom,
                SlideDirection = CurlingSlideDirection.LeftToRight,
                PowerVariancePercent = 0.0,
                WedgePowerMultiplier = 1.0
            };

            var record = CreateTestRecord("golf-wedge-1", strokeNumber: 2);

            // Ball starts at 0.50 (fairway), pin at 0.84 (dist = 0.34)
            GolfSimulationEngine.SimulateGolfShot(
                record,
                settings,
                rawPower: 50.0,
                condition: GolfGreenCondition.StandardGreen,
                targetDistancePercent: 84.0,
                strokeNumber: 2,
                startAxisPosOverride: 0.50,
                rng: new Random(42));

            // At 50% power, wedge should travel ~0.30 (30 yards) toward the pin (lands near 0.80)
            Assert.True(record.LandingX >= 0.75 && record.LandingX <= 0.85, $"Expected LandingX near 0.80, got {record.LandingX}");
            // Must have airborne keyframes (parabolic chip arc)
            Assert.Contains(record.Keyframes, k => k.Z > 0.01);
            Assert.Equal(0.0, record.Keyframes[^1].Z);
        }

        [Fact]
        public void SimulateGolfShot_WedgePowerMultiplier_ScalesDistanceCorrectly()
        {
            var settingsBase = new GolfGameSettings
            {
                StripPosition = CurlingStripPosition.Bottom,
                SlideDirection = CurlingSlideDirection.LeftToRight,
                PowerVariancePercent = 0.0,
                WedgePowerMultiplier = 1.0
            };
            var settingsBoosted = new GolfGameSettings
            {
                StripPosition = CurlingStripPosition.Bottom,
                SlideDirection = CurlingSlideDirection.LeftToRight,
                PowerVariancePercent = 0.0,
                WedgePowerMultiplier = 1.5
            };

            var recordBase = CreateTestRecord("golf-wedge-base", strokeNumber: 2);
            var recordBoosted = CreateTestRecord("golf-wedge-boost", strokeNumber: 2);

            GolfSimulationEngine.SimulateGolfShot(
                recordBase,
                settingsBase,
                rawPower: 40.0,
                condition: GolfGreenCondition.StandardGreen,
                targetDistancePercent: 84.0,
                strokeNumber: 2,
                startAxisPosOverride: 0.30,
                rng: new Random(42));

            GolfSimulationEngine.SimulateGolfShot(
                recordBoosted,
                settingsBoosted,
                rawPower: 40.0,
                condition: GolfGreenCondition.StandardGreen,
                targetDistancePercent: 84.0,
                strokeNumber: 2,
                startAxisPosOverride: 0.30,
                rng: new Random(42));

            var distBase = recordBase.LandingX - 0.30;
            var distBoosted = recordBoosted.LandingX - 0.30;

            Assert.True(distBoosted > distBase, $"Boosted distance {distBoosted} should be greater than base {distBase}");
            Assert.InRange(distBoosted / distBase, 1.4, 1.6);
        }

        [Fact]
        public void SimulateGolfShot_PutterOnGreen_IsSmoothGroundRoll()
        {
            var settings = new GolfGameSettings
            {
                StripPosition = CurlingStripPosition.Bottom,
                SlideDirection = CurlingSlideDirection.LeftToRight,
                PowerVariancePercent = 0.0,
                PutterPowerMultiplier = 1.0
            };

            var record = CreateTestRecord("golf-putt-1", strokeNumber: 2);

            // Ball is on green at 0.82, pin at 0.84 (dist = 0.02 <= 0.035)
            GolfSimulationEngine.SimulateGolfShot(
                record,
                settings,
                rawPower: 20.0,
                condition: GolfGreenCondition.StandardGreen,
                targetDistancePercent: 84.0,
                strokeNumber: 2,
                startAxisPosOverride: 0.82,
                rng: new Random(42));

            // Pure ground roll: no airborne keyframes
            Assert.All(record.Keyframes, k => Assert.Equal(0.0, k.Z));
        }

        [Fact]
        public void SimulateGolfShot_DriverPowerMultiplier_ScalesDistanceLinearly()
        {
            var settingsBase = new GolfGameSettings
            {
                StripPosition = CurlingStripPosition.Bottom,
                SlideDirection = CurlingSlideDirection.LeftToRight,
                PowerVariancePercent = 0.0,
                DriverPowerMultiplier = 1.0
            };
            var settingsBoosted = new GolfGameSettings
            {
                StripPosition = CurlingStripPosition.Bottom,
                SlideDirection = CurlingSlideDirection.LeftToRight,
                PowerVariancePercent = 0.0,
                DriverPowerMultiplier = 1.25
            };

            var recordBase = CreateTestRecord("golf-driver-base", strokeNumber: 1);
            var recordBoosted = CreateTestRecord("golf-driver-boost", strokeNumber: 1);

            GolfSimulationEngine.SimulateGolfShot(
                recordBase,
                settingsBase,
                rawPower: 60.0,
                condition: GolfGreenCondition.StandardGreen,
                targetDistancePercent: 84.0,
                strokeNumber: 1,
                startAxisPosOverride: 0.05,
                rng: new Random(42));

            GolfSimulationEngine.SimulateGolfShot(
                recordBoosted,
                settingsBoosted,
                rawPower: 60.0,
                condition: GolfGreenCondition.StandardGreen,
                targetDistancePercent: 84.0,
                strokeNumber: 1,
                startAxisPosOverride: 0.05,
                rng: new Random(42));

            var distBase = recordBase.LandingX - 0.05;
            var distBoosted = recordBoosted.LandingX - 0.05;

            Assert.InRange(distBoosted / distBase, 1.23, 1.27);
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
