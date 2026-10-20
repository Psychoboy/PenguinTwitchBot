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
