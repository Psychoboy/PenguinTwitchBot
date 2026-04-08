using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Bot.Commands.Alias.Requests
{
    public class UpdateAlias(AliasModel alias) : IRequest
    {
        public AliasModel Alias { get; } = alias;
    }
}
