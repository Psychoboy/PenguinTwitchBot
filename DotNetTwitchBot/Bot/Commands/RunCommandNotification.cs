using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands
{
    public class RunCommandNotification : Application.Notifications.INotification
    {
        public CommandEventArgs? EventArgs { get; set; } = null;
    }
}
