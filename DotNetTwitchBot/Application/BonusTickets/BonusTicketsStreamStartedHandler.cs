using DotNetTwitchBot.Bot.Commands.TicketGames;
using DotNetTwitchBot.Bot.Notifications;

namespace DotNetTwitchBot.Application.BonusTickets
{
    public class BonusTicketsStreamStartedHandler(IBonusTickets bonusTickets) : Application.Notifications.INotificationHandler<StreamStartedNotification>
    {
        public Task Handle(StreamStartedNotification notification, CancellationToken cancellationToken)
        {
            return bonusTickets.Reset();
        }
    }
}
