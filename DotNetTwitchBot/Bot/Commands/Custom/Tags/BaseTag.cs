using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Custom.Tags
{
    public class BaseTag : Application.Notifications.IRequest<CustomCommandResult>
    {
        public CommandEventArgs CommandEventArgs { get; set; } = null!;
        public string Args { get; set; } = "";
    }
}
