
namespace DotNetTwitchBot.Bot.Commands.Alias.Requests
{
    public class GetAliasByName(string name) : Application.Notifications.IRequest<AliasModel?>
    {
        public string Name { get; } = name;
    }
}
