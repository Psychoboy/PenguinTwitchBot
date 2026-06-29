using PenguinTwitchBot.Application.BonusTickets;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Notifications;
using NSubstitute;
using Xunit;

namespace PenguinTwitchBot.Test.Application.BonusTickets
{
    public class BonusTicketsStreamStartedHandlerTests
    {
        [Fact]
        public async Task Handle_InvokesReset()
        {
            var bonusTickets = Substitute.For<IBonusTickets>();
            var handler = new BonusTicketsStreamStartedHandler(bonusTickets);
            var notification = new StreamStartedNotification();

            await handler.Handle(notification, CancellationToken.None);

            await bonusTickets.Received(1).Reset();
        }

        [Fact]
        public async Task Handle_CancellationTokenIsAccepted()
        {
            var bonusTickets = Substitute.For<IBonusTickets>();
            var handler = new BonusTicketsStreamStartedHandler(bonusTickets);
            var notification = new StreamStartedNotification();
            var cts = new CancellationTokenSource();

            await handler.Handle(notification, cts.Token);

            await bonusTickets.Received(1).Reset();
        }

        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(BonusTicketsStreamStartedHandler).GetMethod("Handle"));
        }
    }
}