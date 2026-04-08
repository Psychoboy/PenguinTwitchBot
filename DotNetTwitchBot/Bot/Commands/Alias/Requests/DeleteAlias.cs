
namespace DotNetTwitchBot.Bot.Commands.Alias.Requests
{
    public class DeleteAlias(AliasModel alias) : Application.Notifications.IRequest
    {
        public AliasModel Alias { get; } = alias;
    }
}
