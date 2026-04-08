using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Bot.Commands
{
    public class RunCommandNotification : INotification
    {
        public CommandEventArgs? EventArgs { get; set; } = null;
    }
}
