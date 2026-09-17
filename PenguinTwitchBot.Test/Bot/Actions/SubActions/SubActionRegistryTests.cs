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
                SubActionCategories.VariablesAndUtilities
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
        [InlineData(SubActionTypes.ChannelPointSetEnabledState, SubActionCategories.PointsAndRewards)]
        [InlineData(SubActionTypes.Uptime, SubActionCategories.TwitchAndViewers)]
        [InlineData(SubActionTypes.SetVariable, SubActionCategories.VariablesAndUtilities)]
        public void SubActions_AreAssignedToExpectedCategories(SubActionTypes type, string expectedCategory)
        {
            var meta = SubActionRegistry.GetMetadata(type);
            Assert.NotNull(meta);
            Assert.Equal(expectedCategory, meta.Category);
        }
    }
}

