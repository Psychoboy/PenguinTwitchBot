using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Bot.Commands.Alias.Requests
{
    public class GetAliasByName(string name) : IRequest<AliasModel?>
    {
        public string Name { get; } = name;
    }
}
