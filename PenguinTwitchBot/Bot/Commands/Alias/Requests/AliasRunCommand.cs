using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.Alias.Requests
{
    public class AliasRunCommand : Application.Notifications.IRequest<bool>
    {
        public CommandEventArgs? EventArgs { get; set; } = null;
    }
}
