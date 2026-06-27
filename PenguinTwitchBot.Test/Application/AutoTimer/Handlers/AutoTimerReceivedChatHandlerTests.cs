using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Application.AutoTimer.Handlers;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Commands.Misc;
using NSubstitute;
using Xunit;

namespace PenguinTwitchBot.Test.Application.AutoTimer.Handlers;

public class AutoTimerReceivedChatHandlerTests
{
    [Fact]
    public async Task Handle_InvokesOnChatMessage()
    {
        var autoTimers = Substitute.For<IAutoTimers>();
        var handler = new AutoTimerReceivedChatHandler(autoTimers);
        var notification = new ReceivedChatMessage();

        await handler.Handle(notification, CancellationToken.None);

        await autoTimers.Received(1).OnChatMessage();
    }
}
