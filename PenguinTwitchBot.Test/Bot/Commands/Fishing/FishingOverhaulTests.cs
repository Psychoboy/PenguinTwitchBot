using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Helpers;

namespace PenguinTwitchBot.Test.Bot.Commands.Fishing
{
    public class FishingOverhaulTests
    {
        [Theory]
        [InlineData(50L, "50")]
        [InlineData(4250L, "4,250")]
        [InlineData(48500L, "48.5K")]
        [InlineData(500000000L, "500M")]
        [InlineData(1000000069L, "1B")]
        [InlineData(1250000000L, "1.25B")]
        [InlineData(2500000000000L, "2.5T")]
        public void FormatCompactNumber_FormatsLargeNumbersCleanly(long input, string expected)
        {
            Assert.Equal(expected, FishingFormatHelper.FormatCompactNumber(input));
        }

        [Fact]
        public void AudioResolver_RespectsPriority_PerFishOverGlobalOverDefault()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fishing_audio_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                File.WriteAllBytes(Path.Combine(tempDir, "shot_fish.mp3"), [0x01, 0x02]);
                var resolver = new FishingAudioResolver(tempDir);

                var settings = new FishingSettings
                {
                    SoundEnabled = true,
                    SoundVolume = 0.75,
                    CatchCommonSoundPath = "/sounds/custom-common.mp3",
                    CatchLegendarySoundPath = "/sounds/custom-legendary.mp3"
                };

                // 1. Per-fish custom file beats global override
                var withCustomFish = resolver.ResolveForCatch(settings, FishRarity.Legendary, "shot_fish_medium.webp");
                Assert.Equal("/fishes/shot_fish.mp3", withCustomFish.CatchSoundUrl);

                // 2. Global override beats bundled default
                var withGlobalOverride = resolver.ResolveForCatch(settings, FishRarity.Legendary, "sailfish_medium.webp");
                Assert.Equal("/sounds/custom-legendary.mp3", withGlobalOverride.CatchSoundUrl);

                // 3. Bundled default used when neither per-fish nor global override is set
                var defaultSettings = new FishingSettings();
                var withDefault = resolver.ResolveForCatch(defaultSettings, FishRarity.Rare, "swordfish_medium.webp");
                Assert.Equal(FishingAudioResolver.DefaultCatchRareSoundUrl, withDefault.CatchSoundUrl);
                Assert.Equal(FishingAudioResolver.DefaultCastSoundUrl, withDefault.CastSoundUrl);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void CatchRewardCalculator_EvaluatesFlatGoldAndBonuses()
        {
            var pastiesId = Guid.NewGuid();
            var ticketsId = Guid.NewGuid();

            var goldScaledRule = new FishingCatchRewardRule
            {
                Enabled = true,
                PointTypeId = pastiesId,
                Mode = FishingCatchRewardMode.GoldScaled,
                BaseMultiplier = 1000,
                Star1Multiplier = 1,
                Star2Multiplier = 2,
                Star3Multiplier = 4,
                NewDexBonusPercent = 50
            };

            var mythicalTicketRule = new FishingCatchRewardRule
            {
                Enabled = true,
                PointTypeId = ticketsId,
                MinRarity = FishRarity.Mythical,
                Mode = FishingCatchRewardMode.Flat,
                FlatAmount = 50
            };

            // 3-star 100g Mythical + New Dex (+50%): 100 * 1000 * 4 * 1.5 = 600,000
            var pastiesAmount = FishingCatchRewardCalculator.EvaluateRule(
                goldScaledRule, FishRarity.Mythical, ["Mythical"], 3, 50.0, 100, isNewSpecies: true, isPersonalBest: false);
            Assert.Equal(600_000L, pastiesAmount);

            var ticketOnMythical = FishingCatchRewardCalculator.EvaluateRule(
                mythicalTicketRule, FishRarity.Mythical, ["Mythical"], 3, 50.0, 100, isNewSpecies: true, isPersonalBest: false);
            Assert.Equal(50L, ticketOnMythical);

            var ticketOnCommon = FishingCatchRewardCalculator.EvaluateRule(
                mythicalTicketRule, FishRarity.Common, ["Fish"], 1, 1.0, 10, isNewSpecies: false, isPersonalBest: false);
            Assert.Equal(0L, ticketOnCommon);
        }

        [Fact]
        public void SpecificFishBoost_OnlyBoostsTargetFish_NotOtherFishInSameRarity()
        {
            var blueWhale = new FishType { Id = Guid.NewGuid(), Name = "Blue Whale", Rarity = FishRarity.Legendary, Enabled = true, BaseGold = 500, BaseWeight = 100000 };
            var otherLegendary = new FishType { Id = Guid.NewGuid(), Name = "Kraken", Rarity = FishRarity.Legendary, Enabled = true, BaseGold = 500, BaseWeight = 5000 };
            var commonFish = new FishType { Id = Guid.NewGuid(), Name = "Minnow", Rarity = FishRarity.Common, Enabled = true, BaseGold = 10, BaseWeight = 1 };

            var pool = new List<FishType> { blueWhale, otherLegendary, commonFish };

            var baseWeights = FishingCalculations.ComputeEffectiveFishWeights(pool, new FishingSettings(), [])
                .ToDictionary(x => x.Fish.Id, x => x.Weight);

            var whaleLure = new UserFishingBoost
            {
                UserId = "u1",
                IsEquipped = true,
                RemainingUses = 5,
                ShopItem = new FishingShopItem
                {
                    Name = "Blue Whale Lure",
                    BoostType = FishingBoostType.SpecificFishBoost,
                    BoostAmount = 2.0, // +200% = 3x
                    TargetFishTypeId = blueWhale.Id
                }
            };

            var boostedWeights = FishingCalculations.ComputeEffectiveFishWeights(pool, new FishingSettings(), [whaleLure])
                .ToDictionary(x => x.Fish.Id, x => x.Weight);

            // Blue Whale weight tripled
            Assert.Equal(baseWeights[blueWhale.Id] * 3.0, boostedWeights[blueWhale.Id], precision: 5);
            // Kraken (same Legendary rarity) is completely unboosted!
            Assert.Equal(baseWeights[otherLegendary.Id], boostedWeights[otherLegendary.Id], precision: 5);
        }

        [Fact]
        public void ProgressiveRarityScaling_BoostsHigherRaritiesMoreThanUncommon()
        {
            var weights = FishingRarityWeightProfiles.CreateBaseWeights();
            var baseUncommon = weights[FishRarity.Uncommon];
            var baseLegendary = weights[FishRarity.Legendary];
            var baseMythical = weights[FishRarity.Mythical];

            FishingRarityWeightProfiles.ApplyProgressiveGeneralRarityBoost(weights, 0.5);

            Assert.Equal(baseUncommon * 1.25, weights[FishRarity.Uncommon], precision: 5);
            Assert.Equal(baseLegendary * 2.0, weights[FishRarity.Legendary], precision: 5);
            Assert.Equal(baseMythical * 2.25, weights[FishRarity.Mythical], precision: 5);
        }

        [Fact]
        public async Task FrenzyService_StartsAndStopsAndBroadcasts()
        {
            var ws = Substitute.For<IWebSocketMessenger>();
            var backbone = Substitute.For<IServiceBackbone>();
            var fishingService = Substitute.For<IFishingService>();
            fishingService.GetSettings().Returns(new FishingSettings());
            var audio = Substitute.For<IFishingAudioResolver>();
            audio.ResolveFrenzySoundUrl(Arg.Any<FishingSettings?>()).Returns("/media/fishing_frenzy.mp3");
            var logger = Substitute.For<ILogger<FishingFrenzyService>>();

            using var svc = new FishingFrenzyService(ws, backbone, fishingService, audio, logger);

            Assert.Null(svc.GetActiveFrenzy());

            var state = await svc.StartFrenzyAsync("Saltwater Hotspot", 10, 2.0, "Salt Water", 0.5, announceInChat: true);
            Assert.NotNull(svc.GetActiveFrenzy());
            Assert.Equal("Saltwater Hotspot", state.Name);
            await ws.Received(1).AddToQueue(WsTopics.Fishing, Arg.Is<string>(s => s.Contains("fishingFrenzy") && s.Contains("Saltwater Hotspot")));

            var stopped = await svc.StopFrenzyAsync(announceInChat: true);
            Assert.True(stopped);
            Assert.Null(svc.GetActiveFrenzy());
        }

        [Fact]
        public async Task FrenzyService_StopFrenzyForTournament_OnlyStopsMatchingTournament()
        {
            var ws = Substitute.For<IWebSocketMessenger>();
            var backbone = Substitute.For<IServiceBackbone>();
            var fishingService = Substitute.For<IFishingService>();
            fishingService.GetSettings().Returns(new FishingSettings());
            var audio = Substitute.For<IFishingAudioResolver>();
            audio.ResolveFrenzySoundUrl(Arg.Any<FishingSettings?>()).Returns("/media/fishing_frenzy.mp3");
            var logger = Substitute.For<ILogger<FishingFrenzyService>>();

            using var svc = new FishingFrenzyService(ws, backbone, fishingService, audio, logger);

            var linkedId = Guid.CreateVersion7();
            var otherId = Guid.CreateVersion7();

            await svc.StartFrenzyAsync("Blitz Frenzy", 10, 1.75, null, 0.0, announceInChat: false, linkedTournamentId: linkedId);
            Assert.Equal(linkedId, svc.GetActiveFrenzy()?.LinkedTournamentId);

            // Ending an unrelated tournament must not stop this frenzy
            Assert.False(await svc.StopFrenzyForTournamentAsync(otherId));
            Assert.NotNull(svc.GetActiveFrenzy());

            // Ending the linked tournament stops the paired frenzy cleanly
            Assert.True(await svc.StopFrenzyForTournamentAsync(linkedId));
            Assert.Null(svc.GetActiveFrenzy());
        }

        [Fact]
        public async Task OverlayAckService_CompletesOnNotifyAndTimesOutCleanly()
        {
            var svc = new FishingOverlayAckService();
            var id1 = Guid.CreateVersion7();
            svc.Register(id1);

            var waitTask = svc.WaitForRevealAsync(id1, TimeSpan.FromSeconds(5));
            Assert.True(svc.NotifyRevealed(id1));
            Assert.True(await waitTask);

            var id2 = Guid.CreateVersion7();
            svc.Register(id2);
            Assert.False(await svc.WaitForRevealAsync(id2, TimeSpan.FromMilliseconds(30)));
        }

        [Fact]
        public async Task OverlayDisplaySettings_NormalizesClampsAndBroadcastsOnSave()
        {
            var gameSettings = Substitute.For<PenguinTwitchBot.Bot.Commands.Games.IGameSettingsService>();
            var ws = Substitute.For<IWebSocketMessenger>();
            var svc = new FishingOverlayDisplaySettingsService(gameSettings, ws);

            var custom = new FishingOverlayDisplaySettings
            {
                FrenzyBannerMode = "InvalidMode",
                TournamentOverlayMode = FishingOverlayDisplaySettings.ModePeriodic,
                FrenzyReminderIntervalMinutes = 0,
                FrenzyReminderDurationSeconds = 999,
                TournamentReminderIntervalMinutes = 10,
                TournamentReminderDurationSeconds = 20,
                AutoAlwaysThresholdMinutes = 500
            };

            await svc.SaveAsync(custom);
            var loaded = await svc.GetAsync();

            Assert.Equal(FishingOverlayDisplaySettings.ModeAuto, loaded.FrenzyBannerMode);
            Assert.Equal(FishingOverlayDisplaySettings.ModePeriodic, loaded.TournamentOverlayMode);
            Assert.Equal(1, loaded.FrenzyReminderIntervalMinutes);
            Assert.Equal(60, loaded.FrenzyReminderDurationSeconds);
            Assert.Equal(120, loaded.AutoAlwaysThresholdMinutes);
            await ws.Received(1).AddToQueue(WsTopics.Fishing, Arg.Is<string>(s => s.Contains("fishingOverlayConfig")));
        }

        [Fact]
        public async Task FrenzyService_DoesNotActivateForAlreadyEndedTournament()
        {
            var ws = Substitute.For<IWebSocketMessenger>();
            var backbone = Substitute.For<IServiceBackbone>();
            var fishingService = Substitute.For<IFishingService>();
            fishingService.GetSettings().Returns(new FishingSettings());
            var audio = Substitute.For<IFishingAudioResolver>();
            audio.ResolveFrenzySoundUrl(Arg.Any<FishingSettings?>()).Returns("/media/fishing_frenzy.mp3");
            var logger = Substitute.For<ILogger<FishingFrenzyService>>();

            using var svc = new FishingFrenzyService(ws, backbone, fishingService, audio, logger);

            var endedTournamentId = Guid.CreateVersion7();
            Assert.False(await svc.StopFrenzyForTournamentAsync(endedTournamentId));

            var state = await svc.StartFrenzyAsync("Late Blitz Frenzy", 10, 1.75, null, 0.0, announceInChat: true, linkedTournamentId: endedTournamentId);
            Assert.False(state.IsActiveAt(DateTime.UtcNow));
            Assert.Null(svc.GetActiveFrenzy());
            await ws.DidNotReceive().AddToQueue(WsTopics.Fishing, Arg.Any<string>());
        }

        [Fact]
        public async Task FrenzyService_GetSettingsFailure_PreservesPreviousFrenzy()
        {
            var ws = Substitute.For<IWebSocketMessenger>();
            var backbone = Substitute.For<IServiceBackbone>();
            var fishingService = Substitute.For<IFishingService>();
            fishingService.GetSettings().Returns(
                _ => Task.FromResult<FishingSettings?>(new FishingSettings()),
                _ => Task.FromException<FishingSettings?>(new InvalidOperationException("DB unavailable")));
            var audio = Substitute.For<IFishingAudioResolver>();
            audio.ResolveFrenzySoundUrl(Arg.Any<FishingSettings?>()).Returns("/media/fishing_frenzy.mp3");
            var logger = Substitute.For<ILogger<FishingFrenzyService>>();

            using var svc = new FishingFrenzyService(ws, backbone, fishingService, audio, logger);

            await svc.StartFrenzyAsync("Initial Frenzy", 10, 2.0, null, 0.0, announceInChat: false);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.StartFrenzyAsync("Failing Frenzy", 10, 3.0, null, 0.0, announceInChat: false));

            Assert.Equal("Initial Frenzy", svc.GetActiveFrenzy()?.Name);
        }

        [Fact]
        public void FishingSettings_Clone_ReturnsDetachedCopy()
        {
            var original = new FishingSettings { DisplayDurationMs = 5000, BoostMode = false, CastAnimationMs = 1600 };
            var copy = original.Clone();
            copy.DisplayDurationMs = 9999;
            copy.BoostMode = true;

            Assert.Equal(5000, original.DisplayDurationMs);
            Assert.False(original.BoostMode);
        }

        [Fact]
        public void FishingType_Attempts_SupportsNumbersAndVariables()
        {
            var action = new PenguinTwitchBot.Database.Bot.Actions.SubActions.Types.FishingType();

            // Variable expression accepted & persisted via Text
            var varValues = new Dictionary<string, object?> { ["Attempts"] = "%attempts%" };
            Assert.Null(action.Validate(varValues));
            action.SetValues(varValues);
            Assert.Equal("%attempts%", action.Text);
            Assert.Equal("%attempts%", action.GetValues()["Attempts"]);

            // Numeric value accepted & clears Text
            var numValues = new Dictionary<string, object?> { ["Attempts"] = "5" };
            Assert.Null(action.Validate(numValues));
            action.SetValues(numValues);
            Assert.Equal(5, action.Attempts);
            Assert.Equal(string.Empty, action.Text);
            Assert.Equal("5", action.GetValues()["Attempts"]);

            // Invalid text rejected
            Assert.NotNull(action.Validate(new Dictionary<string, object?> { ["Attempts"] = "abc" }));
            Assert.NotNull(action.Validate(new Dictionary<string, object?> { ["Attempts"] = "99" }));
        }
    }
}
