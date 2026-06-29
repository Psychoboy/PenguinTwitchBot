using PenguinTwitchBot.Application.LoyaltyFeature.Handlers;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Commands.Features;
using NSubstitute;
using Xunit;
using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Test.Application.LoyaltyFeature.Handlers
{
    public class LoyaltyChatMessageHandlerTests
    {
        [Fact]
        public async Task Handle_InvokesOnChatMessage()
        {
            var loyaltyFeature = Substitute.For<ILoyaltyFeature>();
            var handler = new LoyaltyChatMessageHandler(loyaltyFeature);
            var eventArgs = new ChatMessageEventArgs { Name = "testUser", Message = "testMessage" };
            var notification = new ReceivedChatMessage { EventArgs = eventArgs };

            await handler.Handle(notification, CancellationToken.None);

            await loyaltyFeature.Received(1).OnChatMessage(eventArgs);
        }

        [Fact]
        public async Task Handle_CancellationTokenIsAccepted()
        {
            var loyaltyFeature = Substitute.For<ILoyaltyFeature>();
            var handler = new LoyaltyChatMessageHandler(loyaltyFeature);
            var eventArgs = new ChatMessageEventArgs { Name = "testUser", Message = "testMessage" };
            var notification = new ReceivedChatMessage { EventArgs = eventArgs };
            var cts = new CancellationTokenSource();

            await handler.Handle(notification, cts.Token);

            await loyaltyFeature.Received(1).OnChatMessage(eventArgs);
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(LoyaltyChatMessageHandler).GetMethod("Handle"));
        }
    }
}