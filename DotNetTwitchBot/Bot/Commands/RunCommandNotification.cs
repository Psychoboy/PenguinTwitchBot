using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands
{
    public class RunCommandNotification : INotification
    {
        public CommandEventArgs? EventArgs { get; set; } = null;
    }
}
