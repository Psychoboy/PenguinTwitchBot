using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Bot.Commands.Alias.Requests
{
    public class AliasRunCommand : IRequest<bool>
    {
        public CommandEventArgs? EventArgs { get; set; } = null;
    }
}
