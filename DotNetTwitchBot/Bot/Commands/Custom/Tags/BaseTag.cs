using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Tags
{
    public class BaseTag : IRequest<CustomCommandResult>
    {
        public CommandEventArgs CommandEventArgs { get; set; } = null!;
        public string Args { get; set; } = "";
    }
}
