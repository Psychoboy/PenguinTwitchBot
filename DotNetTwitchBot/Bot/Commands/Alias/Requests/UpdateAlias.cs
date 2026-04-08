
namespace DotNetTwitchBot.Bot.Commands.Alias.Requests
{
    public class UpdateAlias(AliasModel alias) : Application.Notifications.IRequest
    {
        public AliasModel Alias { get; } = alias;
    }
}
