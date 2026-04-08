using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Bot.Commands.Custom.Tags
{
    public class BaseTag : IRequest<CustomCommandResult>
    {
        public CommandEventArgs CommandEventArgs { get; set; } = null!;
        public string Args { get; set; } = "";
    }
}
