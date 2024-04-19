using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Alias.Requests
{
    public class AliasRunCommand : IRequest<bool>
    {
        public CommandEventArgs? EventArgs { get; set; } = null;
    }
}
