using PenguinTwitchBot.Application.WheelSpinNotifications;
using PenguinTwitchBot.Bot.Commands.WheelSpin;
using PenguinTwitchBot.Bot.Features;
using NSubstitute;
using Xunit;

namespace PenguinTwitchBot.Test.Application.WheelSpinNotifications
{
    public class WheelSpinCompleteHandlerTests
    {
        [Fact]
        public async Task Handle_InvokesValidateAndProcessWinner()
        {
            var wheelService = Substitute.For<IWheelService>();
            var featureRuntimeCoordinator = Substitute.For<IFeatureRuntimeCoordinator>();
            featureRuntimeCoordinator.IsEnabled(FeatureKeys.WheeledGame).Returns(true);
            var handler = new WheelSpinCompleteHandler(wheelService, featureRuntimeCoordinator);
            var wheelComplete = new WheelSpinComplete { Index = 5 };
            var notification = new WheelSpinCompleteNotification(wheelComplete);

            await handler.Handle(notification, CancellationToken.None);

            await wheelService.Received(1).ValidateAndProcessWinner(5);
        }

        [Fact]
        public async Task Handle_CancellationTokenIsAccepted()
        {
            var wheelService = Substitute.For<IWheelService>();
            var featureRuntimeCoordinator = Substitute.For<IFeatureRuntimeCoordinator>();
            featureRuntimeCoordinator.IsEnabled(FeatureKeys.WheeledGame).Returns(true);
            var handler = new WheelSpinCompleteHandler(wheelService, featureRuntimeCoordinator);
            var wheelComplete = new WheelSpinComplete { Index = 10 };
            var notification = new WheelSpinCompleteNotification(wheelComplete);
            var cts = new CancellationTokenSource();

            await handler.Handle(notification, cts.Token);

            await wheelService.Received(1).ValidateAndProcessWinner(10);
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(WheelSpinCompleteHandler).GetMethod("Handle"));
        }

        [Fact]
        public void WheelSpinCompleteNotification_PropertyExists()
        {
            var wheelComplete = new WheelSpinComplete { Index = 42 };
            var notification = new WheelSpinCompleteNotification(wheelComplete);
            Assert.NotNull(notification.WheelSpinComplete);
        }

        [Fact]
        public void WheelSpinComplete_IndexPropertyWorks()
        {
            var wheelComplete = new WheelSpinComplete { Index = 99 };
            Assert.Equal(99, wheelComplete.Index);
        }
    }
}