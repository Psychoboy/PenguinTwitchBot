using PenguinTwitchBot.Application.Alert.Handlers;
using PenguinTwitchBot.Application.Alert.Notification;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.Alerts;
using NSubstitute;
using Xunit;

namespace PenguinTwitchBot.Test.Application.Alert.Handlers
{
    public class QueueAlertHandlerTests
    {
        [Fact]
        public async Task Handle_InvokesAddToQueue()
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var handler = new QueueAlertHandler(webSocketMessenger);
            var alertSound = new AlertSound { Path = "test", AudioHook = "test.mp3" };
            var alert = alertSound.Generate();
            var notification = new QueueAlert(alert);

            await handler.Handle(notification, CancellationToken.None);

            await webSocketMessenger.Received(1).AddToQueue(alert);
        }

        [Fact]
        public async Task Handle_UsesAlertFromNotification()
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var handler = new QueueAlertHandler(webSocketMessenger);
            var customAlert = "custom_alert_json_string";
            var notification = new QueueAlert(customAlert);

            await handler.Handle(notification, CancellationToken.None);

            await webSocketMessenger.Received(1).AddToQueue(customAlert);
        }

        [Fact]
        public async Task Handle_CancellationTokenIsAccepted()
        {
            var webSocketMessenger = Substitute.For<IWebSocketMessenger>();
            var handler = new QueueAlertHandler(webSocketMessenger);
            var alertSound = new AlertSound { Path = "test", AudioHook = "test.mp3" };
            var alert = alertSound.Generate();
            var notification = new QueueAlert(alert);
            var cts = new CancellationTokenSource();

            await handler.Handle(notification, cts.Token);

            await webSocketMessenger.Received(1).AddToQueue(alert);
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(QueueAlertHandler).GetMethod("Handle"));
        }

        [Fact]
        public void NotificationAlertProperty_GetterReturnsValue()
        {
            var notification = new QueueAlert("testAlert");
            Assert.Equal("testAlert", notification.Alert);
        }
    }
}