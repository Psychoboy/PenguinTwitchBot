using DotNetTwitchBot.Bot.Commands.TicketGames;
using DotNetTwitchBot.Bot.Notifications;
using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Application.BonusTickets
{
    public class BonusTicketsStreamStartedHandler(IBonusTickets bonusTickets) : INotificationHandler<StreamStartedNotification>
    {
        public Task Handle(StreamStartedNotification notification, CancellationToken cancellationToken)
        {
            return bonusTickets.Reset();
        }
    }
}
