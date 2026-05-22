using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Notifications;

namespace PenguinTwitchBot.Application.BonusTickets
{
    public class BonusTicketsStreamStartedHandler(IBonusTickets bonusTickets) : Application.Notifications.INotificationHandler<StreamStartedNotification>
    {
        public Task Handle(StreamStartedNotification notification, CancellationToken cancellationToken)
        {
            return bonusTickets.Reset();
        }
    }
}
