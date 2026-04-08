using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Alias.Requests
{
    public class AliasRunCommand : Application.Notifications.IRequest<bool>
    {
        public CommandEventArgs? EventArgs { get; set; } = null;
    }
}
