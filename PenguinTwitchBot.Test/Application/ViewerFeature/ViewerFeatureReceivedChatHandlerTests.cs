using PenguinTwitchBot.Application.ViewerFeature;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Commands.Features;
using NSubstitute;
using Xunit;
using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Test.Application.ViewerFeature
{
    public class ViewerFeatureReceivedChatHandlerTests
    {
        [Fact]
        public async Task Handle_InvokesOnChatMessage()
        {
            var viewerFeature = Substitute.For<IViewerFeature>();
            var handler = new ViewerFeatureReceivedChatHandler(viewerFeature);
            var eventArgs = new ChatMessageEventArgs { Name = "testUser", Message = "testMessage" };
            var notification = new ReceivedChatMessage { EventArgs = eventArgs };

            await handler.Handle(notification, CancellationToken.None);

            await viewerFeature.Received(1).OnChatMessage(eventArgs);
        }

        [Fact]
        public async Task Handle_CancellationTokenIsAccepted()
        {
            var viewerFeature = Substitute.For<IViewerFeature>();
            var handler = new ViewerFeatureReceivedChatHandler(viewerFeature);
            var eventArgs = new ChatMessageEventArgs { Name = "testUser", Message = "testMessage" };
            var notification = new ReceivedChatMessage { EventArgs = eventArgs };
            var cts = new CancellationTokenSource();

            await handler.Handle(notification, cts.Token);

            await viewerFeature.Received(1).OnChatMessage(eventArgs);
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(ViewerFeatureReceivedChatHandler).GetMethod("Handle"));
        }
    }
}