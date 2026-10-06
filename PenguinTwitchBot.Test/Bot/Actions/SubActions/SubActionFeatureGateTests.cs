using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class SubActionFeatureGateTests
    {
        [Fact]
        public void IsAvailable_WhenOpenAiNotConfigured_ReturnsFalse()
        {
            var coordinator = Substitute.For<IFeatureRuntimeCoordinator>();
            coordinator.HasFeature(FeatureKeys.OpenAI).Returns(false);
            coordinator.IsEnabled(FeatureKeys.OpenAI).Returns(true);

            var result = SubActionFeatureGate.IsAvailable(SubActionTypes.OpenAi, coordinator);

            Assert.False(result);
        }

        [Fact]
        public void IsAvailable_WhenOpenAiConfiguredAndEnabled_ReturnsTrue()
        {
            var coordinator = Substitute.For<IFeatureRuntimeCoordinator>();
            coordinator.HasFeature(FeatureKeys.OpenAI).Returns(true);
            coordinator.IsEnabled(FeatureKeys.OpenAI).Returns(true);

            var result = SubActionFeatureGate.IsAvailable(SubActionTypes.OpenAi, coordinator);

            Assert.True(result);
        }

        [Fact]
        public void IsAvailable_WhenOpenAiConfiguredButDisabled_ReturnsFalse()
        {
            var coordinator = Substitute.For<IFeatureRuntimeCoordinator>();
            coordinator.HasFeature(FeatureKeys.OpenAI).Returns(true);
            coordinator.IsEnabled(FeatureKeys.OpenAI).Returns(false);

            var result = SubActionFeatureGate.IsAvailable(SubActionTypes.OpenAi, coordinator);

            Assert.False(result);
        }

        [Fact]
        public void IsAvailable_UngatedSubAction_ReturnsTrue()
        {
            var coordinator = Substitute.For<IFeatureRuntimeCoordinator>();

            var result = SubActionFeatureGate.IsAvailable(SubActionTypes.SendMessage, coordinator);

            Assert.True(result);
        }
    }
}

