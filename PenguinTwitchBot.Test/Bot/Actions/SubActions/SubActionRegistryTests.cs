using Xunit;
using PenguinTwitchBot.Database.Bot.Actions.SubActions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using System.Linq;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class SubActionRegistryTests
    {
        [Fact]
        public void AllRegisteredSubActions_HaveValidCategory()
        {
            var validCategories = new[]
            {
                SubActionCategories.ChatAndMedia,
                SubActionCategories.Fishing,
                SubActionCategories.LogicAndFlow,
                SubActionCategories.Obs,
                SubActionCategories.OverlayTimer,
                SubActionCategories.PointsAndRewards,
                SubActionCategories.Raffles,
                SubActionCategories.TwitchAndViewers,
                SubActionCategories.VariablesAndUtilities,
                SubActionCategories.Ai,
                SubActionCategories.Minigames
            };

            var allMetadata = SubActionRegistry.Metadata.Values.ToList();
            Assert.NotEmpty(allMetadata);

            foreach (var meta in allMetadata)
            {
                Assert.False(string.IsNullOrWhiteSpace(meta.Category), $"SubAction {meta.EnumValue} has empty category");
                Assert.Contains(meta.Category, validCategories);
            }
        }

        [Theory]
        [InlineData(SubActionTypes.ObsSetScene, SubActionCategories.Obs)]
        [InlineData(SubActionTypes.ObsTriggerHotkey, SubActionCategories.Obs)]
        [InlineData(SubActionTypes.Fishing, SubActionCategories.Fishing)]
        [InlineData(SubActionTypes.FishingTournamentStart, SubActionCategories.Fishing)]
        [InlineData(SubActionTypes.RaffleStart, SubActionCategories.Raffles)]
        [InlineData(SubActionTypes.RaffleEnd, SubActionCategories.Raffles)]
        [InlineData(SubActionTypes.OverlayTimerStart, SubActionCategories.OverlayTimer)]
        [InlineData(SubActionTypes.Delay, SubActionCategories.LogicAndFlow)]
        [InlineData(SubActionTypes.LogicIfElse, SubActionCategories.LogicAndFlow)]
        [InlineData(SubActionTypes.SendMessage, SubActionCategories.ChatAndMedia)]
        [InlineData(SubActionTypes.PlaySound, SubActionCategories.ChatAndMedia)]
        [InlineData(SubActionTypes.CheckPoints, SubActionCategories.PointsAndRewards)]
        [InlineData(SubActionTypes.ManagePoints, SubActionCategories.PointsAndRewards)]
        [InlineData(SubActionTypes.ChannelPointSetEnabledState, SubActionCategories.PointsAndRewards)]
        [InlineData(SubActionTypes.Uptime, SubActionCategories.TwitchAndViewers)]
        [InlineData(SubActionTypes.SetVariable, SubActionCategories.VariablesAndUtilities)]
        [InlineData(SubActionTypes.OpenAi, SubActionCategories.Ai)]
        [InlineData(SubActionTypes.ParachuteDrop, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.ParachuteDroplets, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.PlinkoDrop, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.StartDropGameSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.StopDropGameSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.ParachuteStartSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.ParachuteStopSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.PlinkoStartSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.PlinkoStopSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.BalloonStartSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.BalloonStopSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.CurlingStartSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.CurlingStopSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.GolfStartSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.GolfStopSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.RacingStartSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.RacingStopSession, SubActionCategories.Minigames)]
        [InlineData(SubActionTypes.RacingJoin, SubActionCategories.Minigames)]
        public void SubActions_AreAssignedToExpectedCategories(SubActionTypes type, string expectedCategory)
        {
            var meta = SubActionRegistry.GetMetadata(type);
            Assert.NotNull(meta);
            Assert.Equal(expectedCategory, meta.Category);
        }

        [Fact]
        public void StartDropGameSession_IsHiddenFromPicker()
        {
            var meta = SubActionRegistry.GetMetadata(SubActionTypes.StartDropGameSession);
            Assert.NotNull(meta);
            Assert.True(meta.IsHiddenFromPicker);
        }

        [Theory]
        [InlineData("Parachute", SubActionTypes.ParachuteStartSession)]
        [InlineData("Plinko", SubActionTypes.PlinkoStartSession)]
        [InlineData("Balloon", SubActionTypes.BalloonStartSession)]
        [InlineData("Curling", SubActionTypes.CurlingStartSession)]
        [InlineData("Golf", SubActionTypes.GolfStartSession)]
        [InlineData("Racing", SubActionTypes.RacingStartSession)]
        public void MinigameSubActionMigrationHelper_UpgradesLegacyStartSession(string gameType, SubActionTypes expectedType)
        {
            var legacy = new StartDropGameSessionType
            {
                Id = 42,
                Index = 3,
                Enabled = true,
                Text = "legacy",
                GameType = gameType,
                DurationSeconds = "90",
                ThemeOverrideId = "neon"
            };

            var upgraded = MinigameSubActionMigrationHelper.UpgradeLegacySubAction(legacy);
            Assert.Equal(expectedType, upgraded.SubActionTypes);
            Assert.Equal(42, upgraded.Id);
            Assert.Equal(3, upgraded.Index);
            Assert.True(upgraded.Enabled);
        }

        [Theory]
        [InlineData("Parachute", SubActionTypes.ParachuteStopSession)]
        [InlineData("Plinko", SubActionTypes.PlinkoStopSession)]
        [InlineData("Balloon", SubActionTypes.BalloonStopSession)]
        [InlineData("Curling", SubActionTypes.CurlingStopSession)]
        [InlineData("Golf", SubActionTypes.GolfStopSession)]
        [InlineData("Racing", SubActionTypes.RacingStopSession)]
        [InlineData("All", SubActionTypes.StopDropGameSession)]
        public void MinigameSubActionMigrationHelper_UpgradesLegacyStopSession(string gameType, SubActionTypes expectedType)
        {
            var legacy = new StopDropGameSessionType
            {
                Id = 99,
                Index = 1,
                Enabled = true,
                GameType = gameType
            };

            var upgraded = MinigameSubActionMigrationHelper.UpgradeLegacySubAction(legacy);
            Assert.Equal(expectedType, upgraded.SubActionTypes);
            Assert.Equal(99, upgraded.Id);
            Assert.Equal(1, upgraded.Index);

            if (upgraded is PenguinTwitchBot.Database.Bot.Actions.SubActions.UI.ISubActionUIProvider provider &&
                provider.GetValues().TryGetValue("ClearQueue", out var clearQueueValue))
            {
                Assert.Equal(false, clearQueueValue);
            }
        }

        [Fact]
        public void StopDropGameSessionType_SetValues_FallsBackToAllWhenGameTypeNotString()
        {
            var stopAll = new StopDropGameSessionType();
            stopAll.SetValues(new System.Collections.Generic.Dictionary<string, object?> { ["GameType"] = null });
            Assert.Equal("All", stopAll.GameType);
        }
    }
}

