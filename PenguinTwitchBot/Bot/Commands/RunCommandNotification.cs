using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands
{
    public class RunCommandNotification : Application.Notifications.INotification
    {
        public CommandEventArgs? EventArgs { get; set; } = null;
    }
}
